# JobScheduler — A Guide for Game Programmers

This guide assumes you write Unity gameplay code — MonoBehaviours, `Update`, coroutines or UniTask — and have never written multithreaded code, or have and got burned. It explains the ideas behind `JobScheduler` from the ground up: what a thread is, why sharing data between threads goes wrong, how this system avoids that without locks, and what each building block (jobs, batches, chunks, phases, `FrameContext`) is for. It ends with how to choose an approach for a given problem and a complete worked feature.

The companion [`README.md`](README.md) in this folder is the reference: exact API, ownership tables, internals. Read this guide first; use the README when you need precise rules.

---

## Part 1 — Why threads, and why they are dangerous

### 1.1 The main thread is your frame budget

At 60 frames per second a frame is 16.6 milliseconds. Everything in your `Update` methods, plus Unity's own work — animation, physics, rendering setup, UI layout — runs one after another on a single CPU core called the **main thread**. If your AI pass takes 5 ms, it has eaten 30% of the frame before anything else has happened. Phones make this worse: their cores are slow, and they throttle when hot.

Meanwhile a modern phone has six to eight cores, and your gameplay code uses one of them.

### 1.2 What a thread is

A thread is a second worker running code at the same time as the main thread, usually on another core. Two threads really do execute simultaneously — this is not like coroutines, which take turns on one thread. That is the whole appeal: a 5 ms AI pass on a worker thread costs the main thread roughly nothing.

### 1.3 Why you cannot just start a `Thread`

Two things go wrong immediately.

**The Unity API is main-thread-only.** `transform.position`, `Time.deltaTime`, `Physics.Raycast`, `Debug.Log` in a loop, `Random.value` — engine calls check which thread they are on and throw, or worse, silently return garbage. Any code on a worker thread must be *pure C#*: your own classes, arrays, lists, math.

**Data races.** Suppose the main thread is looping over `enemies` in `Update` while a worker adds an enemy to the same list. The list resizes its internal array halfway through the loop and you get an `InvalidOperationException: Collection was modified` — on a good day. On a bad day you read a half-copied array and get a wrong enemy with no error at all.

Even a single integer is not safe. `gold += 10` is three steps: read `gold`, add ten, write it back. If two threads do this at once, both read 100, both write 110, and one payment is lost. Nothing crashes. The bug appears once per thousand runs, only on the player's device, and never in the editor.

The rule underneath all of this: **two threads must never touch the same memory at the same time if either of them is writing.** Two readers are fine. One writer plus anyone else is a race.

### 1.4 The classic fix, and why we do not use it

The textbook answer is a **lock**: a door only one thread can be behind at a time. `lock (enemies) { ... }` makes everyone else wait outside until you leave.

Locks work, and they are how most server software is written. In a game loop they are a bad trade:

- **Waiting is the enemy.** If the main thread waits on a lock held by a worker for 2 ms, that is a 2 ms hitch. On a phone the OS may pause the worker while it holds the lock (the main thread is more important, so the worker is descheduled) and the hitch becomes 20 ms.
- **They are easy to get subtly wrong.** Forget one lock and you have a rare race. Take two locks in different orders in two places and you have a deadlock.
- **They cost even when uncontended**, and a per-element lock in a loop over 10,000 elements costs more than the work.

`JobScheduler` contains no locks at all. Not because locks are evil, but because a game frame has a structure that makes them unnecessary.

### 1.5 The alternative: do not share, hand over

Think of a job as an **envelope**. You put the inputs in, seal it, and hand it to a worker. While the worker has it, you do not touch anything inside — not to read, not to write. The worker does the work, writes the answer inside the same envelope, seals it, and hands it back. Now it is yours again, and the worker does not touch it.

At any moment the envelope has exactly one owner. There is nothing to lock because nobody ever reaches for the same thing at the same time.

The one remaining question is *when* envelopes change hands. If hand-overs could happen at arbitrary moments you would be back to needing a lock to make the hand-over itself safe. So the scheduler allows hand-overs at exactly one moment per frame: the **barrier**.

---

## Part 2 — How the scheduler thinks

### 2.1 The barrier

Once per frame, at the very start (before any script's `Update`), the scheduler runs the barrier on the main thread. At that moment every worker is idle — it has finished last frame's envelopes and is waiting. Because nobody else is running, the main thread can safely:

1. take back all the envelopes the workers finished and deliver the answers to you;
2. hand the workers everything you scheduled during the last frame;
3. ring the bell — the workers wake up and start.

Then the barrier returns, your `Update` methods run, and the workers work *at the same time* on the envelopes they were just given. You cannot interfere with them because you no longer hold those envelopes, and they cannot interfere with you because they only hold what you gave them.

### 2.2 The frame timeline, with a concrete example

An enemy decides it needs a path during frame 100.

```
frame 100   your Update:  scheduler.Schedule(pathJob)        the envelope goes in the "pending" pile (main thread owns it)
─ barrier ─ top of 101:   pending pile → workers               workers now own it
frame 101   workers:      pathJob.Execute(...) runs             meanwhile your Update runs normally on the main thread
─ barrier ─ top of 102:   finished pile → main thread           pathJob.OnComplete(false) is called; you read the path
frame 102   your Update:  the enemy starts walking
```

So the fixed cadence is: **schedule in frame N, runs during N+1, answer at the start of N+2.** At 60 fps that is about 33 ms from asking to knowing. For AI decisions, steering, simulation, procedural generation and scoring, 33 ms is invisible. For things the player feels directly — input response, camera — it is not, and those should not go through jobs at all.

### 2.3 "Frozen"

Between two barriers, nothing about the workers' world changes: the set of jobs they are running is fixed, and the data those jobs read must be fixed too. The scheduler guarantees the first part — a job you schedule during frame 101 is *not* handed to the workers until the barrier at the top of 102, even if a worker is sitting idle. It looks wasteful; it is what makes the system correct without locks.

The second part — the data a job reads not changing under it — is your responsibility, and Part 4 is about how to arrange it.

### 2.4 Waiting, blocking, skipping

Three words that sound similar and mean different things here.

**Waiting** is a thread doing nothing until something happens. The workers wait for the bell between frames. That is fine: they have nothing else to do, and an idle thread costs nothing.

**Blocking** is the main thread waiting. That is a frame hitch, and the scheduler never does it on its own. The only calls that block are `WaitForIdle()` and `Dispose()`, meant for scene changes and shutdown.

**Skipping** is what happens if the workers are *still busy* when the next barrier arrives, because a job took longer than a frame. The scheduler does not wait for them. It skips the barrier entirely: nothing is collected, nothing is handed over, your pending envelopes stay in the pile, and it tries again next frame. Results arrive a frame late; `Stats.SkippedFrames` counts it. The old `JobDispatcher` this system replaced blocked the main thread here with a five-second timeout — one slow job froze the game.

### 2.5 What goes in, what comes out

Every field on a job is one of four kinds, and being clear about which is which is most of the skill:

| Kind | Who writes it | Who reads it | Example |
|---|---|---|---|
| **Input** | main thread, before scheduling | worker, in `Execute` | enemy position, target |
| **Output** | worker, in `Execute` | main thread, in `OnComplete` or from `Results` | the path, a score, a steering vector |
| **Scratch** | worker | worker | the open list of an A* search — allocated once, reused every run |
| **Pass-through** | main thread | main thread | the `Enemy` object the answer belongs to — carried in the envelope, never touched by the worker |

An input that is a *reference* (a list, an array, an object) needs one more decision: does the main thread give it away, or keep using it? Part 4.

### 2.6 Threads never talk

There is no message-passing here, no queue of events between threads. Threads agree on *places* (fields, arrays) and *times* (the barrier). "Coordination" means: the main thread puts data in an agreed place before the barrier; the worker reads it after; the worker writes results in an agreed place before the next barrier; the main thread reads them after. That is all coordination ever is in this system.

---

## Part 3 — The building blocks

### 3.1 A job, and its clock

A job is any class or struct implementing `IJob`:

```csharp
public sealed class SortScoresJob : IJob
{
    public int[] Scores;                                   // input: given to the job

    public void Execute(in FrameContext ctx)               // runs on a worker thread
    {
        Array.Sort(Scores);
        Array.Reverse(Scores);
    }
}
```

`FrameContext` is the only clock a worker may read. `Time.deltaTime` and friends are engine calls and off-limits; the scheduler copies them at the barrier and hands every job the same frozen values:

| Field | Same as | Use it for |
|---|---|---|
| `Frame` | `Time.frameCount` | debugging, "which frame's answer is this" |
| `Time` | `Time.time` | timers and cooldowns inside jobs |
| `UnscaledTime` | `Time.unscaledTime` | effects that ignore pause / slow-motion |
| `DeltaTime` | `Time.deltaTime` | integration: `position += velocity * ctx.DeltaTime` |

When the game is paused with `Time.timeScale = 0`, `ctx.DeltaTime` is 0, so a simulation job naturally stands still.

### 3.2 One-shot jobs and the reply

`Schedule(job)` runs a job once, next frame. To get the answer back, also implement `IJobCallback`:

```csharp
public sealed class SortScoresJob : IJob, IJobCallback
{
    public int[]       Scores;                             // input, handed over
    public Action<int[]> Published;                        // pass-through: what to do with the answer

    public void Execute(in FrameContext ctx)  { Array.Sort(Scores); Array.Reverse(Scores); }

    public void OnComplete(bool cancelled)                 // runs on the main thread, exactly once
    {
        if (!cancelled) Published(Scores);                 // safe to touch UI, transforms, anything
    }
}

_scheduler.Schedule(new SortScoresJob { Scores = copyOfScores, Published = leaderboard.Show });
```

`OnComplete` is your notification that the envelope is back. It runs on the main thread, inside the barrier, before the workers are woken for the next frame — so at that moment the object is entirely yours. `cancelled == true` means "do not use the outputs": either you cancelled it, or the scheduler was shut down. It does *not* guarantee the job never ran, so `OnComplete` should never assume anything about the outputs when `cancelled` is true.

Notice `copyOfScores`: the job sorts the array in place, so the main thread must not be using that array meanwhile. Either give it away (and stop using it), or give the job a copy.

### 3.3 Handles

`Schedule` returns a `JobHandle`, a small value — think coat-check ticket:

```csharp
JobHandle h = _scheduler.Schedule(job);
_scheduler.IsPending(h);     // true until OnComplete has been delivered
_scheduler.Cancel(h);        // withdraw it; OnComplete(true) will fire once
```

A ticket for a coat that has already been collected does nothing: `Cancel` and `IsPending` on a handle whose job completed return `false`, and never accidentally affect a *different* job that later reused the same internal slot. You can keep handles around carelessly. `JobHandle.Invalid` (the default value) is a ticket for nothing and is always safe to pass.

### 3.4 Repeating jobs

`ScheduleRepeating(job)` runs the job every frame from the next one until you `Cancel` it. It has no per-frame reply; `OnComplete(true)` fires once when it is withdrawn.

The catch: a repeating job's fields are being written by a worker in *every* frame, so there is never a moment when the main thread may read them. Repeating jobs are for work whose output stays on the worker side — feeding a later phase (3.7) — or that publishes through a batch. If the main thread needs the value every frame, you want a batch, not a repeating job. Most people who reach for `ScheduleRepeating` actually want 3.5.

### 3.5 Batches — many small identical jobs

Suppose 500 enemies each need the same computation every frame. You could `Schedule` 500 job objects, but that is 500 envelopes to hand over and 500 objects scattered around memory. A **batch** stores them as one array of structs:

```csharp
public struct SightJob : IJob
{
    public Vector2 Eye, Player;         // inputs
    public bool    CanSee;              // output

    public void Execute(in FrameContext ctx) => CanSee = Vector2.Distance(Eye, Player) < 12f;
}

JobBatch<SightJob> batch = new(capacity: 512);
_scheduler.Register(batch);            // once
```

Why structs in an array are so much faster than objects: they sit next to each other in memory, so when a core reads element 0 it has already pulled elements 1–3 into its cache for free, and it can see it will need 4–7 and fetch them ahead of time. Objects live wherever the allocator put them, and each one is a separate trip to main memory — on a phone, dozens of nanoseconds each, thousands of times per frame.

A batch owns **three arrays** and rotates them at every barrier:

```
during frame N      you Add() into  ─► [ fill ]         workers run   [ execute ]      you read   [ results ]
barrier N+1         rotate:  fill becomes execute,  execute becomes results,  old results becomes the new fill
```

So the elements you add in frame N run during N+1 and are readable from `Results` from the barrier at the top of N+2. `Results` is stable for the whole frame — read it in `Update`, `LateUpdate`, wherever. If a frame had nothing to run, `Results` keeps last frame's data instead of going empty.

```csharp
void Update()
{
    // read last finished frame
    foreach (ref readonly SightJob r in batch.Results) { /* apply r.CanSee */ }

    // produce next frame — but only if the previous production has been handed over
    if (batch.PendingCount != 0) return;

    Vector2 player = _player.position;
    foreach (Enemy e in _enemies)
    {
        ref SightJob j = ref batch.Add();          // reserves a cleared element, fill it in place
        j.Eye = e.EyePosition; j.Player = player;
    }
}
```

**The `PendingCount` rule** is the one thing everyone gets wrong once. `PendingCount` is how many elements you have added since the last barrier. It is normally 0 at the start of your `Update` because the barrier just took them. It is *not* 0 if the barrier skipped (2.4) — the workers were still busy, so your elements are still waiting. If you add another 500 on top, the next hand-over is 1,000 elements, the workers take twice as long, the barrier skips again, and now it is 1,500. The system does not break, but it gets slower every frame. Adding only when `PendingCount == 0` means a slow frame costs one frame of staleness and nothing else.

### 3.6 Chunks — how a batch is shared between workers

The scheduler does not hand a 10,000-element batch to one worker. It cuts it into **chunks** — contiguous slices — and every worker grabs the next unclaimed chunk when it finishes its current one.

Why not simply four chunks of 2,500 for four workers? Because one worker may get a slow core (phones have big and little cores), or the OS may pause it for a moment, and then three workers sit idle while the fourth finishes. With sixteen chunks of 625, the fast workers take more chunks and the frame finishes when the *work* is done, not when the slowest worker is done. The default aims for `ChunksPerWorker = 4` chunks per worker, and never cuts a batch finer than `MinBatchChunkItems = 16` elements, because claiming a chunk costs a little and a chunk of three elements is not worth it.

You can pin the chunk size with the `itemsPerChunk` constructor argument. Do it only when measuring; the default is right far more often than a guess.

### 3.7 Phases — ordering inside a frame

Normally all jobs in a frame run in any order, all at once. Sometimes job B needs the *result* of job A within the same frame. **Phases** provide that: with `PhaseCount = 2`, every job and batch element of phase 0 finishes before any of phase 1 starts. A phase-1 job can therefore read what phase 0 wrote.

```csharp
var scheduler = new JobScheduler(new JobSchedulerOptions { PhaseCount = 2 });
var move      = new JobBatch<MoveJob>(500, phase: 0);        // computes where each agent wants to go
var separate  = new JobBatch<SeparateJob>(500, phase: 1);    // pushes agents apart using everyone's phase-0 answer
```

Phases are a dividing line inside the frame; there is no notion of "job B depends on job A" individually. That is deliberate — a dependency graph is where job systems become complicated, and two or three phases cover the integrate → resolve → post-process shape that games actually have.

Two things to weigh before adding a phase. First, workers idle at the boundary: if phase 0 has one long job, everyone waits for it before phase 1 can begin. Second, very often phase 1 could just use *last frame's* results instead — neighbours' positions one frame old are fine for steering — and then you need no phase at all. Reach for phases when the data must be from *this* frame and the consumer is heavy enough to belong on a worker. Part 7 shows both choices.

### 3.8 Workers — how many

`WorkerCount` is the number of worker threads. The default is `cores − 2`, clamped to between 1 and 4. Two cores are left for Unity itself: the main thread and the render thread (and Unity's own job workers, and the OS). More managed workers than that on a phone tend to fight each other for cores rather than help. The right number comes from measuring on the target device with the benchmark scene in `UGFW/Examples/Source/Jobs`; `Stats` tells you what you are getting (3.9).

### 3.9 Seeing it work

`scheduler.Stats` is a snapshot of the last finished frame: how many jobs and chunks ran, how many threw, how many frames were skipped so far, total worker time, and `LastFrameCriticalTicks` — the time of the *slowest* worker, which is how long the parallel work really took. If critical time is close to total worker time, you are not getting parallelism (one giant job); if `SkippedFrames` climbs, something is longer than a frame.

In the Unity Profiler's timeline the workers appear as a thread group named **AK.Jobs**, each with an `AK.Jobs.Worker` block per frame; the barrier is `AK.Jobs.Tick` on the main thread. That is the fastest way to *see* whether your work moved off the main thread.

---

## Part 4 — Moving data between threads safely

The single rule from 1.3 again: **nobody writes what someone else might be reading or writing at the same time.** Every safe pattern below is a way of satisfying it. Pick one per input; mixing them is fine.

### 4.1 Copy in

Small inputs — positions, targets, speeds — are copied into the job's fields before scheduling. The job owns its copy; the original can change freely afterwards. This is the default and covers most inputs. Batch elements are entirely copy-in by nature.

### 4.2 Hand over

A collection the job needs to read or modify at length — a list of candidates to score, an array to sort — is given to the job, and **the main thread stops using it until `OnComplete`**. If the main thread still needs it meanwhile, give the job a copy instead (`list.ToArray()`, or copy into a buffer the job owns and reuses).

```csharp
_job.Candidates = _candidates;            // handed over
_candidates = _spareList;                 // main thread continues with the other list
_scheduler.Schedule(_job);                // in OnComplete, swap them back
```

### 4.3 Share read-only

Data that *nobody* writes while jobs run can be shared by everyone: the level's wall segments, a string catalogue, a lookup table. When it has to change, do not mutate it — build a new array and point new jobs at it. Jobs in flight keep the old one alive and finish against it. This is the cheapest pattern and the one most often broken by a later "small fix" that mutates the array in place.

### 4.4 Partition

A shared array where element *i* writes **only slot *i***. Every slot has exactly one writer, so there is no race even though the array is shared. This is how batch elements communicate outward beyond their own fields, and how phase 0 leaves results for phase 1:

```csharp
public struct MoveJob : IJob
{
    public int       Index;
    public Vector2   Position, Target;
    public float     Speed;
    public Vector2[] Desired;                 // shared; this element writes only Desired[Index]

    public void Execute(in FrameContext ctx) => Desired[Index] = Vector2.MoveTowards(Position, Target, Speed * ctx.DeltaTime);
}
```

The main thread must not read `Desired` during the frame — the workers are writing it. It is worker-only scratch that the main thread merely allocated.

### 4.5 Phase hand-off

Phase 0 writes (by partition), phase 1 reads. The phase boundary guarantees all writes are finished before any read starts:

```csharp
public struct SeparateJob : IJob
{
    public int       Index, Count;
    public float     Radius;
    public Vector2[] Desired;                 // written by phase 0 this frame; read-only here
    public Vector2   Final;                   // output

    public void Execute(in FrameContext ctx)
    {
        Vector2 me = Desired[Index], push = Vector2.zero;
        for (int i = 0; i < Count; i++)
        {
            if (i == Index) continue;
            Vector2 away = me - Desired[i];
            float   d    = away.magnitude;
            if (d > 0f && d < Radius) push += away / d * (Radius - d);
        }
        Final = me + push;
    }
}
```

### 4.6 Freeze a changing input with two buffers

The hard case: every element needs to read data that the main thread updates every frame — all the other agents' positions, say. Copy-in is quadratic; sharing is a race because the main thread writes next frame's positions while workers read this frame's.

The answer is two copies, used alternately: the main thread writes into the one the workers are *not* reading. The `PendingCount == 0` rule from 3.5 is exactly what tells you which one is free. (`FlockJob` here is a batch element with `Index`, `Position`, a `Neighbours` reference to the snapshot, and a `Steering` output; the full struct is in the README, use case 6.2.)

```csharp
public sealed class Snapshot { public Vector2[] Positions = new Vector2[512]; public int Count; }

private readonly Snapshot[] _snapshots = { new(), new() };
private int _write;

void Update()
{
    foreach (ref readonly FlockJob r in _batch.Results) _boids[r.Index].Apply(r.Steering);

    if (_batch.PendingCount != 0) return;                // barrier skipped: both snapshots may be in use, write nothing

    Snapshot s = _snapshots[_write];                      // the one no running or waiting element references
    _write ^= 1;
    s.Count = _boids.Count;
    for (int i = 0; i < _boids.Count; i++) s.Positions[i] = _boids[i].Position;

    for (int i = 0; i < _boids.Count; i++)
    {
        ref FlockJob j = ref _batch.Add();
        j.Index = i; j.Position = _boids[i].Position; j.Neighbours = s;
    }
}
```

Why this is safe: when `PendingCount == 0` just after a barrier, the elements you added last frame were handed over and are running against snapshot A. The elements from the frame before ran against snapshot B and were collected at this barrier — nobody references B any more. So you write B. If the barrier skipped, `PendingCount` is not 0 and you write nothing, so A and B both stay untouched. Two buffers suffice because at most two productions are ever alive at once — one running, one waiting — and the rule prevents a third.

### 4.7 What is never safe

| Never | Why |
|---|---|
| Mutate a list or array a running job holds | the race from 1.3, in its purest form |
| Read a repeating job's fields from the main thread | a worker writes them every frame; there is no safe moment |
| Schedule the same job object twice while it is still pending | two workers write one object in the same frame |
| Have two batch elements write the same shared slot | two writers |
| Call any Unity API inside `Execute` | engine calls are main-thread-only; `Vector2`/`Vector3`/`Mathf`/`Quaternion` math is fine, `Physics.gravity` or `transform` is not |
| Use `ListPool<T>` or any other main-thread pool inside `Execute` | a pool is shared mutable state |
| Call `Schedule`, `Cancel`, `Add` from inside `Execute` | the scheduler is main-thread-only and will throw; return follow-up work as data and schedule it in `OnComplete` |

---

## Part 5 — Time, pausing, stopping, shutting down

### 5.1 Timers inside jobs

Use `ctx.Time`. A cooldown is a field on the element compared against the frame's time:

```csharp
public struct AlertJob : IJob
{
    public float AlertUntil;        // input, carried from last frame's result
    public bool  SeesPlayer;        // input
    public bool  IsAlert;           // output

    public void Execute(in FrameContext ctx)
    {
        if (SeesPlayer) AlertUntil = ctx.Time + 3f;
        IsAlert = ctx.Time < AlertUntil;
    }
}
```

Because `Results` is mutable, last frame's `AlertUntil` can be copied straight into next frame's element — the batch becomes a little state machine that runs off the main thread.

### 5.2 Game pause

Pausing with `Time.timeScale = 0` makes `ctx.DeltaTime` 0, so integration jobs stand still by themselves. They still *run*, though — burning a little battery to compute nothing. In a pause menu, stop producing: skip the `Add` loop, and `Results` keeps showing the last state (it is sticky). One-shots you do not schedule do not run. That is all "pausing" is for batches and one-shots: stop feeding them.

### 5.3 Pausing and resuming a repeating job

There is no pause switch; `Cancel` it, and `ScheduleRepeating` it again later:

```csharp
_fog = _scheduler.ScheduleRepeating(_fogJob);     // running every frame
_scheduler.Cancel(_fog);                          // stops from the next barrier; OnComplete(true) fires once
// ... later, even in the same frame:
_fog = _scheduler.ScheduleRepeating(_fogJob);     // resumes from the next barrier; same object, no allocation
```

Cancelling and re-scheduling the same object in the same frame is safe: both changes take effect at the same barrier, so it is never in the running set twice.

### 5.4 Stopping work

- **One-shot:** `Cancel(handle)`. If it has not been handed over yet it never runs; if it has, it may finish this frame. Either way `OnComplete(true)` arrives once at the next barrier.
- **Batch:** `Unregister(batch)` stops it rotating. A frame already in flight still runs it, so do not throw the batch away until `scheduler.IsIdle` or after a `WaitForIdle()` — acceptable on a loading screen, never per frame.
- **Everything:** `Dispose()` (5.5).

### 5.5 Shutdown

An app-lifetime scheduler attached with `AttachToPlayerLoop()` disposes itself when the application quits and, in the editor, when you leave play mode. `Dispose` waits up to two seconds for the frame in flight, delivers its answers, then delivers `OnComplete(true)` to everything that never ran, stops the worker threads and unregisters every batch. Nothing is force-killed. After that, every call throws `ObjectDisposedException` — so a `MonoBehaviour` cleaning up in `OnDestroy` should check first:

```csharp
void OnDestroy()
{
    if (_batch.IsRegistered) _scheduler.Unregister(_batch);   // false once the scheduler has been disposed
}
```

Worker threads are background threads, so even a hard kill of the process never hangs on them.

### 5.6 A job longer than a frame

Say a mesh bake takes 30 ms. Frame N+1 starts it; at the barrier of N+2 it is still running, so the barrier skips — and it skips for every other job as well, because the running set is frozen until *all* of it is done. Nothing corrupts, but a steering batch sharing the scheduler now updates every third frame while the bake runs.

Options, in order of preference: split the work into slices that each fit in a frame (bake one chunk per job); give heavy background work its own `JobScheduler` with one worker — nothing stops you running two; or accept it for a rare event like level load, where the screen is a loading bar anyway.

### 5.7 When a job throws

An exception inside `Execute` is caught per job (or per batch element), logged with the full stack trace, and counted in `Stats.LastFrameFaults` (and `batch.ResultFaults`). Other jobs in the frame finish; the worker thread survives; the job's `OnComplete(false)` still fires — so check your outputs there if the job can fail. A `NullReferenceException` or "can only be called from the main thread" in that log almost always means an engine call sneaked into `Execute`.

---

## Part 6 — Choosing an approach

### 6.1 Which tool

| Your work looks like | Use | Because |
|---|---|---|
| The same small computation for hundreds or thousands of things, every frame | `JobBatch<T>` | contiguous structs, chunked across workers, zero allocation |
| A few different, heavier tasks, each with its own inputs and a result | `Schedule` with a reused `IJob` object + `OnComplete` | one envelope per task, answer on the main thread |
| Something that must happen at a button press, a query, a save | the delegate form `Schedule(ctx => ..., done => ...)` | allocates, but at event rate that is irrelevant |
| Worker-side work that feeds other worker-side work every frame | `ScheduleRepeating` + phases | no main-thread round trip |
| Anything touching transforms, UI, physics, audio, animation | the main thread | the engine is main-thread-only |
| Waiting for time, frames, network or disk | UniTask | that is *when*, not *where*; `await UniTask.Delay(...)` then `Schedule` |

### 6.2 Is it worth a job at all?

A job costs a hand-over, a chunk claim, and two frames of latency. If the total work is under a tenth of a millisecond per frame, the main thread is the right place — you would spend more on the hand-over than you save. If the answer is needed *this* frame (input, camera, hit reactions), it cannot be a job. Everything else that takes real time — from about half a millisecond a frame upward — is a candidate.

### 6.3 Typical game work

| Task | Approach | Notes |
|---|---|---|
| Pathfinding requests | one-shot, pooled `PathRequest : IJob, IJobCallback` | grid is share-read-only (4.3); cancel on re-target |
| Crowd / boid steering | batch, phase 0; optional phase 1 for separation | freeze neighbours with two snapshots (4.6) |
| Line-of-sight / awareness for many agents | batch | walls share-read-only; Part 7 |
| AI utility scoring | batch (per agent) or one-shot (per squad) | copy-in inputs |
| Fog of war, influence maps, flow fields | repeating job writing a grid, phase 0; consumers in phase 1; publish the display grid through a one-element batch or `OnComplete` | |
| Procedural mesh / terrain chunk | one-shot with owned `List<Vector3>`/`List<int>`; `OnComplete` uploads to the `Mesh` | slice large bakes (5.6) |
| Save serialization, compression, checksums | delegate one-shot | hand over a copy of the data |
| Sorting / filtering / text search over a big collection | one-shot | replace the catalogue, never mutate it (4.3) |
| Image processing | one-shot on a `Color32[]` from `GetPixels32()` on main | `SetPixels32` + `Apply` in `OnComplete` |
| UI layout, animation, physics queries, audio | main thread | engine-owned |
| Networking, file IO, timers | UniTask | IO waits; it does not compute |

---

## Part 7 — A worked feature: line-of-sight for 500 enemies

### Step 1 — the main-thread version and why it hurts

```csharp
void Update()
{
    Vector2 player = _player.position;
    foreach (Enemy e in _enemies)
    {
        bool canSee = true;
        foreach (Wall w in _walls)
            if (Segments.Intersect(e.EyePosition, player, w.A, w.B)) { canSee = false; break; }
        e.OnSightUpdated(canSee, Vector2.Distance(e.EyePosition, player));
    }
}
```

500 enemies × 200 walls = 100,000 segment tests per frame: two to four milliseconds on a phone, every frame, on the main thread. It is pure math on plain data — the ideal shape for a job. `Segments.Intersect` is an ordinary static function:

```csharp
public static class Segments
{
    public static bool Intersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float d1 = Cross(q2 - q1, p1 - q1), d2 = Cross(q2 - q1, p2 - q1);
        float d3 = Cross(p2 - p1, q1 - p1), d4 = Cross(p2 - p1, q2 - p1);
        return d1 * d2 < 0f && d3 * d4 < 0f;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
```

### Step 2 — the envelope

Decide the four kinds of field (2.5). Inputs: the eye and the player position — copy-in. The walls — share-read-only; the level does not change them while running. Output: `CanSee`, `Distance`. Pass-through: which enemy this is.

```csharp
public struct Wall { public Vector2 A, B; }

public struct SightJob : IJob
{
    public Enemy   Enemy;              // pass-through: the main thread's, never touched in Execute
    public Vector2 Eye, Player;        // inputs, copied
    public Wall[]  Walls;              // input, shared read-only
    public bool    CanSee;             // output
    public float   Distance;           // output

    public void Execute(in FrameContext ctx)
    {
        Distance = Vector2.Distance(Eye, Player);
        CanSee   = true;
        for (int i = 0; i < Walls.Length; i++)
        {
            if (Segments.Intersect(Eye, Player, Walls[i].A, Walls[i].B)) { CanSee = false; return; }
        }
    }
}
```

Carrying the `Enemy` reference rather than an index matters: results arrive two frames after you filled the element, and by then an enemy may have died and the list shifted. A reference stays correct; an index does not.

### Step 3 — the batch

```csharp
public sealed class EnemySight : MonoBehaviour
{
    [Inject] private IJobScheduler _scheduler;

    private JobBatch<SightJob> _batch;
    private Wall[]             _walls;          // built on level load, never modified afterwards
    private List<Enemy>        _enemies;
    private Transform          _player;

    private void Start()
    {
        _batch = new JobBatch<SightJob>(capacity: 512);
        _scheduler.Register(_batch);
    }

    private void OnDestroy()
    {
        if (_batch.IsRegistered) _scheduler.Unregister(_batch);
    }

    private void Update()
    {
        foreach (ref readonly SightJob r in _batch.Results)
        {
            if (r.Enemy.IsAlive) r.Enemy.OnSightUpdated(r.CanSee, r.Distance);
        }

        if (_batch.PendingCount != 0) return;

        Vector2 player = _player.position;
        foreach (Enemy e in _enemies)
        {
            ref SightJob j = ref _batch.Add();
            j.Enemy = e; j.Eye = e.EyePosition; j.Player = player; j.Walls = _walls;
        }
    }
}
```

That is the whole feature. The main thread now spends its time on two loops of 500 trivial assignments; the 100,000 segment tests happen on the workers while `Update` runs. Results are two frames old — 33 ms — which no player will notice in an enemy's awareness.

### Step 4 — the walls change

A door opens; a wall is destroyed. Do not edit `_walls` in place — elements in flight are reading it. Build a new array and assign it: `_walls = BuildWalls();`. Elements already handed over keep the old array alive and finish against it; the next production uses the new one. This is pattern 4.3, and it is the entire cost of dynamic walls.

### Step 5 — "how many enemies see the player?"

Tempting to make a phase-1 job for it. Do not: it is a count over 500 booleans, which the main thread does in microseconds while it is already looping over `Results`. Phases are for work heavy enough to belong on a worker *and* dependent on this frame's other results. Adding one here would buy a sync point and nothing else.

### Step 6 — when a phase is the right answer

Now the enemies move, and they must not overlap. Where each enemy *wants* to go is independent per enemy (phase 0). Pushing overlapping enemies apart needs *everyone's* desired position from *this* frame, and it is 500 × 500 distance checks — far too heavy for the main thread. That is a phase:

```csharp
var scheduler = new JobScheduler(new JobSchedulerOptions { PhaseCount = 2 });
var desired   = new Vector2[512];                                 // partitioned scratch, worker-only
var move      = new JobBatch<MoveJob>(512, phase: 0);             // 4.4: writes desired[Index]
var separate  = new JobBatch<SeparateJob>(512, phase: 1);         // 4.5: reads all of desired, writes Final
scheduler.Register(move);
scheduler.Register(separate);

void Update()
{
    foreach (ref readonly SeparateJob r in separate.Results) _agents[r.Index].Position = r.Final;

    if (move.PendingCount != 0) return;                           // both batches hand over together, one check suffices

    for (int i = 0; i < _agents.Count; i++)
    {
        ref MoveJob m = ref move.Add();
        m.Index = i; m.Position = _agents[i].Position; m.Target = _agents[i].Target; m.Speed = _agents[i].Speed; m.Desired = desired;

        ref SeparateJob s = ref separate.Add();
        s.Index = i; s.Count = _agents.Count; s.Radius = 0.6f; s.Desired = desired;
    }
}
```

The alternative without a phase would be to separate against *last frame's* positions (copied into a snapshot, 4.6). For a crowd walking at a few units per second that is usually indistinguishable, and it removes the sync point. Try the stale version first; add the phase when you can see the difference.

---

## Part 8 — Troubleshooting

**Results are one frame behind what I expected.** That is the design: schedule in N, run in N+1, read in N+2. If a feature cannot tolerate that, it does not belong in a job.

**`NullReferenceException` or "can only be called from the main thread" logged from inside `Execute`.** An engine call is inside the job — `transform`, `Time`, `Physics`, `Random`, a `MonoBehaviour` field. Copy the value into the job on the main thread first.

**`InvalidOperationException: JobScheduler is single-writer`.** You called `Schedule`, `Cancel`, `Register` or `batch.Add` from a worker thread — almost always from inside `Execute`. (`OnComplete` runs on the main thread and may schedule freely; `Execute` may not.) Return the follow-up work as data and schedule it in `OnComplete`.

**`Stats.SkippedFrames` keeps growing.** A job is longer than a frame, or a producer ignored the `PendingCount` rule and the pending pile snowballed. `LastFrameCriticalTicks` says how long the frame's work really took.

**Numbers are occasionally wrong, never reproducibly.** A shared mutable input — somebody edits an array or list that jobs in flight are reading, or two elements write the same slot. Go through every reference-type input against Part 4.

**`OnComplete` never fires.** It always fires exactly once per `Schedule` — with `cancelled == true` if the scheduler was disposed. If you never see it, the job was never scheduled (check the handle is valid) or the scheduler was never attached to the player loop.

**More workers made it slower.** Oversubscription: the workers are fighting Unity's threads for cores. Lower `WorkerCount`; measure with `Stats` on the device.

**Can I `await` a job?** Not directly, but a completion source turns `OnComplete` into a task (UniTask shown; `TaskCompletionSource<T>` works the same way):

```csharp
public sealed class AwaitableJob<T> : IJob, IJobCallback
{
    public Func<T> Work;                                            // pure C#, runs on the worker
    public readonly UniTaskCompletionSource<T> Done = new();
    private T _result;

    public void Execute(in FrameContext ctx) => _result = Work();
    public void OnComplete(bool cancelled)
    {
        if (cancelled) Done.TrySetCanceled(); else Done.TrySetResult(_result);
    }
}

var job = new AwaitableJob<int> { Work = () => CountReachable(grid) };
_scheduler.Schedule(job);
int reachable = await job.Done.Task;                                // resumes on the main thread two frames later
```

**Where do I go from here?** The [`README.md`](README.md) in this folder has the precise rules for every call, five more worked use cases, the internals, and the list of decisions still open — including deferred and interval scheduling, which do not exist yet and which you may find you want.
