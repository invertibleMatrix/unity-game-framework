# JobScheduler

Assembly `AK.Jobs`, namespace `AK.Jobs`. A lock-free, frame-synchronous job system for **managed** code: it runs plain C# on a small pool of worker threads and hands results back at a fixed point in the frame, without ever making the main thread wait.

This is the reference: the long-form companion to the `Module: Jobs` section of `UGFW/README.md`. It goes top-down — mental model, then the API, then usage patterns — and finishes with worked use cases, the internals, and the decisions still open. Read section 2 before anything else; every rule in the rest of the document falls out of it.

If you have not worked with threads or a job system before, start with [`GUIDE.md`](GUIDE.md) instead. It explains the ideas this document takes for granted — threads, data races, ownership, freezing, chunks, phases — from the ground up, with a complete worked feature.

---

## 1. What problem it solves

On a phone the main thread is the budget. Unity's own C# Job System moves work off it, but only work that Burst can compile: blittable structs in `NativeArray`, no classes, no `List<T>`, no strings, no game-model objects — and `JobHandle.Complete()` stalls the main thread when a job is late. Most gameplay-side heavy work is not shaped like that: pathfinding over a grid class, scoring a `List<Candidate>`, baking a mesh into managed arrays, steering a crowd whose state lives in ordinary objects.

`JobScheduler` runs that code on worker threads under one rule:

> **A buffer has exactly one owner at a time, and ownership changes only at the barrier — the moment at the top of the frame when every worker is provably parked.**

Because nothing is ever read and written by two threads in the same frame, there is nothing to lock. The price is one frame of latency. What you get: zero locks, zero per-frame allocations once warmed up, N workers sharing the work, and a worst case of "this result arrives a frame late", never "the frame hitches while the main thread waits".

---

## 2. Mental model

### 2.1 The frame timeline

```
frame N        main: Schedule(job) / batch.Add()            → appended to main-owned pending buffers
top of N+1     BARRIER (start of EarlyUpdate, before any Update)
                 workers idle?  deliver N-1's completions on main, swap pending ⇄ executing, kick workers
                 workers busy?  SKIP: nothing swaps, pending keeps accumulating, results arrive one frame late
frame N+1      workers run the frozen executing set, in parallel with the whole main-thread frame
top of N+2     BARRIER: OnComplete(...) on main; batch.Results readable for the whole frame
```

So the fixed cadence is: **schedule in N → runs during N+1 → result in N+2.** A repeating job runs in every frame from N+1 on; a batch element added in N is readable in `Results` from N+2 on.

### 2.2 Who owns what, when

| Data | During a frame, owned by | Ownership changes |
|---|---|---|
| Pending jobs / pending batch elements | main thread (you append) | at the barrier: becomes the executing set |
| Executing jobs / executing batch elements | workers (each writes only its own job or element) | at the next barrier: collected, or rotated to `Results` |
| `batch.Results` | main thread (read, and mutate if you like) | at a barrier where a newer frame finished |
| A reference job's fields | main before `Schedule`, worker between the two barriers, main again inside `OnComplete` | at each barrier |
| A repeating job's fields | the worker, every frame, until cancelled | at the barrier after `Cancel` |
| `FrameContext` | frozen at the barrier, read-only to everyone | every barrier |

The two barrier events are the memory fences. The only atomics in the system are the chunk cursor, the phase counters, and the "workers remaining" counter.

### 2.3 Where your code runs

| Your code | Thread | Unity API allowed? |
|---|---|---|
| `Schedule`, `Cancel`, `Register`, `batch.Add`, reading `batch.Results`, `Stats` | main (the scheduler's creating thread; anything else throws) | yes |
| `IFrameJob.Execute` | a worker | **no** |
| `IFrameJobCallback.OnComplete` | main, inside the barrier, before workers are kicked | yes |

"Unity API" means engine calls: `UnityEngine.Object` and everything derived from it, `Time`, `Physics`, `NavMesh`, `UnityEngine.Random`, `Resources`, `Debug.Log` in a hot loop. Pure value types — `Vector3`, `Quaternion`, `Mathf`, `Color`, `Bounds` — are just arithmetic and are fine anywhere.

### 2.4 Overrun: skip, never block

If a frame's workers are still running when the next barrier arrives, the barrier does nothing: no collection, no swap, no kick. Pending work stays pending, results arrive a frame later, `Stats.SkippedFrames` goes up. The old `JobDispatcher` blocked the main thread here with a 5 s timeout that killed the worker on expiry; this system trades that for latency. `WaitForIdle()` exists for teardown and scene changes, not for per-frame use.

The consequence for producers is section 5.5's rule: only add to a batch when `PendingCount == 0`, otherwise a skipped frame doubles the next hand-off and the overrun feeds itself.

### 2.5 The three rules for job code

1. **No Unity API** in `Execute`. Read time from `FrameContext`, not `Time`.
2. **No `ListPool<T>`** or any other main-thread pool. In the editor `ListPool.Rent` logs an error when a worker calls it.
3. **Own your scratch memory.** Allocate it once on the job object or in the batch element and reuse it across runs. Never share a mutable collection between a job in flight and the main thread.

And one corollary of section 2.2 that trips people up: **a reference job object may be in the executing set once at a time.** Schedule the same object again only after its `OnComplete` has fired (or gate it on a batch's `PendingCount`, as in 6.3).

---

## 3. The API

Everything lives in `AK.Jobs`. This is the whole public surface.

```csharp
public interface IJobScheduler : IDisposable
{
    FrameJobHandle Schedule(IFrameJob job, int phase = 0);            // runs once, next frame
    FrameJobHandle ScheduleRepeating(IFrameJob job, int phase = 0);   // every frame from the next one until cancelled
    bool           Cancel(FrameJobHandle handle);                     // false if stale or already cancelled
    bool           IsPending(FrameJobHandle handle);                  // true until the completion is delivered

    void Register<TJob>(JobBatch<TJob> batch)   where TJob : struct, IFrameJob;   // rotate it at every barrier
    void Unregister<TJob>(JobBatch<TJob> batch) where TJob : struct, IFrameJob;

    bool IsIdle { get; }
    void WaitForIdle();                                     // blocks; teardown only
    JobSchedulerStats Stats { get; }
}

public sealed class JobScheduler : IJobScheduler
{
    public JobScheduler(JobSchedulerOptions options = null);
    public void AttachToPlayerLoop();                       // ticks the barrier at the start of EarlyUpdate
    public void DetachFromPlayerLoop();
    public bool IsAttachedToPlayerLoop { get; }
    public void Dispose();                                  // collects the frame in flight, joins workers, cancels the rest
}

public sealed class JobSchedulerOptions
{
    public int WorkerCount        = clamp(cores - 2, 1, 4);   // 0: no threads; always 0 on WebGL
    public int PhaseCount         = 1;      // ordered phases per frame (section 5.6)
    public int ChunksPerWorker    = 4;      // automatic chunking target
    public int MinBatchChunkItems = 16;     // never split a batch finer than this
    public int SpinCount          = 20;     // spins before a worker parks in the kernel
}

public interface IFrameJob          { void Execute(in FrameContext ctx); }        // worker thread
public interface IFrameJobCallback  { void OnComplete(bool cancelled); }          // main thread, exactly once per Schedule

public readonly struct FrameContext { int Frame; float Time, UnscaledTime, DeltaTime; }

public readonly struct FrameJobHandle : IEquatable<FrameJobHandle>   // 8 bytes, generational; stale handles are rejected, never aliased
{
    public static readonly FrameJobHandle Invalid;
    public bool IsValid { get; }
}

public sealed class JobBatch<TJob> where TJob : struct, IFrameJob
{
    public JobBatch(int capacity, int phase = 0, int itemsPerChunk = 0);   // 0 = automatic chunking
    public ref TJob    Add();                 // reserve the next element, cleared, filled in place
    public void        Add(in TJob job);
    public void        ClearPending();
    public int         PendingCount { get; }  // added since the last barrier
    public Span<TJob>  Results      { get; }  // last finished frame; stable for the whole frame
    public int         ResultFaults { get; }  // elements in Results whose Execute threw
    public bool        IsRegistered { get; }
}

public readonly struct JobSchedulerStats
{
    int  WorkerCount, LastFrameJobs, LastFrameChunks, LastFrameFaults, SkippedFrames;
    long LastFrameWorkerTicks, LastFrameCriticalTicks;      // Stopwatch ticks; TicksToMilliseconds(long)
}

// Convenience. Allocates a wrapper per call — for event-rate work, not per-frame work.
public static class JobSchedulerExtensions
{
    public static FrameJobHandle Schedule(this IJobScheduler s, Action<FrameContext> execute, Action<bool> onComplete = null, int phase = 0);
    public static FrameJobHandle ScheduleRepeating(this IJobScheduler s, Action<FrameContext> execute, Action<bool> onComplete = null, int phase = 0);
}
```

Two things the signatures do not say. First, the callback is not a parameter: a job that also implements `IFrameJobCallback` gets its `OnComplete` called; a job that does not is fire-and-forget. Second, `Schedule`/`ScheduleRepeating` accept any `IFrameJob`, including a struct boxed once, but the fast path for many small homogeneous jobs is `JobBatch<TJob>`, not many `Schedule` calls.

---

## 4. Setup

```csharp
// In your Reflex installer. AK.Jobs is not auto-referenced: add it to the asmdef that uses it.
public void InstallBindings(ContainerBuilder builder)
{
    var scheduler = new JobScheduler(new JobSchedulerOptions { WorkerCount = 2, PhaseCount = 2 });
    scheduler.AttachToPlayerLoop();
    builder.RegisterValue(scheduler, new[] { typeof(IJobScheduler) });   // Reflex disposes IDisposable values with the container
}

// Anywhere
[Inject] private readonly IJobScheduler _scheduler;
```

`AttachToPlayerLoop` inserts a `JobScheduler.PlayerLoopTick` system at index 0 of `EarlyUpdate`, so the barrier runs before any script's `Update`, and subscribes `Application.quitting` and (in the editor) exit-play-mode to `Dispose`. You therefore normally never call `Dispose` yourself for an app-lifetime scheduler. A scheduler scoped to a scene or a mode should be disposed explicitly when that scope ends; `Dispose` waits up to 2 s for the frame in flight, delivers its completions, then delivers `OnComplete(cancelled: true)` to everything that never ran.

`WorkerCount` defaults to `clamp(cores − 2, 1, 4)`. Unity already runs a render thread and its own job workers; on a big.LITTLE phone an extra managed worker may land on a little core. The default is a starting point until the benchmark scene (`UGFW/Examples/Source/Jobs/JobSchedulerBenchmark.cs`) has been run on the target device. `0` starts no threads: the barrier runs the frame on the main thread, and completions and batch results still arrive at the barrier after, as they do with workers. WebGL, which can't start threads, always runs that way.

---

## 5. Usage patterns

### 5.1 One-shot with a result

The job object carries inputs, outputs and scratch. Main fills inputs, the worker writes outputs, main reads them in `OnComplete`.

```csharp
public sealed class ScoreCandidatesJob : IFrameJob, IFrameJobCallback
{
    public List<Candidate> Candidates;           // in  — handed over; main does not touch it until OnComplete
    public Candidate       Best;                 // out — written by the worker, read on main in OnComplete
    public Action<Candidate> Published;

    public void Execute(in FrameContext ctx)
    {
        float best = float.NegativeInfinity;
        foreach (Candidate c in Candidates)
        {
            float s = Scoring.Evaluate(c, ctx.Time);   // pure C#
            if (s > best) { best = s; Best = c; }
        }
    }

    public void OnComplete(bool cancelled)
    {
        if (!cancelled) Published(Best);
    }
}

FrameJobHandle handle = _scheduler.Schedule(new ScoreCandidatesJob { Candidates = list, Published = ApplyChoice });
```

`OnComplete` is guaranteed exactly once per `Schedule`, on the main thread, inside a barrier. `cancelled == false` means the job ran; `cancelled == true` means it was withdrawn or the scheduler was disposed — and, if the cancel arrived after hand-off, it may still have run once. Treat `cancelled` as "do not use the outputs", not as "it never executed".

### 5.2 Reusing job objects — the zero-allocation steady state

Per-frame work should not allocate a job per frame. Keep the object, refill inputs, schedule again after its previous run completed:

```csharp
private readonly ScoreCandidatesJob _job = new();
private FrameJobHandle _handle;

void Update()
{
    if (_scheduler.IsPending(_handle)) return;        // the previous run has not completed yet
    _job.Candidates = _candidates;
    _handle = _scheduler.Schedule(_job);
}
```

`IsPending` stays true until the completion is delivered, so this pattern runs the job every *other* frame (schedule N, run N+1, complete N+2, schedule N+2). To run every frame, gate on hand-off instead of completion: either keep two job objects and alternate them, or tie the schedule to a batch's `PendingCount == 0` as in 6.3. Steady-state `Schedule` of a reused object allocates nothing on the main thread — `JobSchedulerCoreTests` asserts zero GC allocations over 256 jobs × 50 frames, and zero on every thread over 200 frames of mixed batch, one-shot, repeating and cancel work with 4 workers. Section 5.9 has the numbers for what *does* allocate.

### 5.3 Repeating jobs

```csharp
FrameJobHandle h = _scheduler.ScheduleRepeating(_influenceMapJob, phase: 0);
// ... later
_scheduler.Cancel(h);   // OnComplete(cancelled: true) fires once at the next barrier
```

A repeating job runs in every frame from the next one until cancelled, with no per-frame callback. Its fields are being written by a worker during every frame, **so the main thread must never read them directly** — there is no moment when the object is main-owned. Repeating jobs are for work whose output stays on the worker side (feeding a later phase, section 5.6) or is published through a batch. If the main thread needs a value every frame, use 5.2 with two alternating objects, or a batch.

### 5.4 Cancelling

```csharp
bool withdrawn = _scheduler.Cancel(handle);
```

- Cancelled **before** hand-off: the job never runs.
- Cancelled **after** hand-off: it may run once more this frame; you cannot stop a chunk mid-flight.
- Either way `OnComplete(true)` fires at the next barrier, exactly once.
- A stale handle (job already completed, slot reused by a later job) returns `false` and touches nothing. This is what the generation in `FrameJobHandle` buys: you can keep handles around carelessly.
- `Cancel(FrameJobHandle.Invalid)` returns `false`.

There is no per-job "cancelled" flag a worker checks, deliberately: that would be one more cross-thread field. Cancellation is a main-thread bookkeeping change that takes effect at the barrier.

### 5.5 Batches — the fast path

Homogeneous struct jobs stored contiguously. A worker streams through the array with a direct call per element; there is no object per job and nothing to chase.

```csharp
public struct SteerJob : IFrameJob
{
    public int     Index;                 // which agent this element belongs to
    public Vector3 Position, Target;      // in
    public Vector3 Steering;              // out

    public void Execute(in FrameContext ctx) => Steering = (Target - Position).normalized;
}

private readonly JobBatch<SteerJob> _batch = new(capacity: 512);
_scheduler.Register(_batch);                          // once; idempotent for the same scheduler

void Update()
{
    // 1. consume the last finished frame — stable for this whole frame
    foreach (ref readonly SteerJob r in _batch.Results) _agents[r.Index].Apply(r.Steering);

    // 2. produce next frame's inputs — only if the previous production has been handed off
    if (_batch.PendingCount != 0) return;
    for (int i = 0; i < _agents.Count; i++)
    {
        ref SteerJob j = ref _batch.Add();            // cleared, filled in place, no copy
        j.Index = i; j.Position = _agents[i].Position; j.Target = _agents[i].Target;
    }
}
```

Three buffers rotate at the barrier — fill, execute, read — so filling this frame's inputs and reading last frame's results never touch the same array. `Results` is a mutable `Span<TJob>`, so a result can be edited and fed straight back with `Add(in r)`. `Results` stays valid for the whole frame and is replaced only by a later frame that had at least one element; an empty frame leaves it alone.

**The producer rule.** Add only when `PendingCount == 0`. If the workers overran and the barrier skipped, last frame's elements are still pending; adding another frame's worth makes the next hand-off twice as long, and the overrun feeds itself. Skipping the add re-simulates from the latest `Results` next frame, which is what a simulation wants anyway. The benchmark scene demonstrated the failure mode before this rule: a 46 ms critical path with every frame skipped, from a 4 ms workload.

`itemsPerChunk` pins the chunk size; leave it at 0 unless you are measuring. `ResultFaults` counts elements in `Results` whose `Execute` threw — their fields may be half-written.

### 5.6 Phases

`JobSchedulerOptions.PhaseCount = 2` splits each frame into ordered phases: every chunk of phase 0 finishes before any chunk of phase 1 starts, so a phase-1 job may read what phase 0 wrote. Pass `phase:` to `Schedule`, `ScheduleRepeating` or the `JobBatch` constructor. Within one phase there is no ordering.

This covers integrate → resolve → post-process pipelines without a dependency graph. Data crosses phases through memory both sides can see: a phase-0 element writes `Shared[Index]` (each element owns its index, so no two workers write the same slot), and a phase-1 job reads `Shared`. Section 6.3 is a full example. Scheduling a phase ≥ `PhaseCount` throws `ArgumentOutOfRangeException`.

### 5.7 Delegates, for event-rate work

```csharp
_scheduler.Schedule(ctx => Compress(saveBytes), cancelled => { if (!cancelled) Upload(); });
```

Each call allocates a small wrapper. Fine for a button press or a search query; not for `Update`.

### 5.8 Diagnostics

`Stats` is a snapshot of the last collected frame: jobs and chunks executed, faults, cumulative `SkippedFrames`, total worker time and the slowest worker's time (`LastFrameCriticalTicks`, the frame's parallel critical path). A game can tune `WorkerCount` at runtime from those two numbers. In the Profiler timeline the workers appear as the thread group **AK.Jobs** with the marker `AK.Jobs.Worker`; the barrier is `AK.Jobs.Tick` on the main thread. Worker threads are named `AK.Jobs Worker 0..N-1` and are background threads, so a hard application kill never hangs on them.

An exception in `Execute` is logged with `Debug.LogException`, counted in `LastFrameFaults` (and `ResultFaults` for batches), and never stops the other jobs or the worker.

### 5.9 GC footprint

Measured in the editor (Mono, 64-bit) with the profiler's `GC.Alloc` recorder across all threads.

| When | Allocates | Notes |
|---|---|---|
| Steady state: `Schedule`/`Cancel` of reused jobs, batch fill + `Results`, barrier, chunk claims, phase waits, job execution | **nothing**, on any thread | asserted by three tests; 287/287 suite |
| `new JobScheduler` | ~61 objects / ~8 KB (1 worker, 1 phase); ~111 / ~16 KB (4 workers, 2 phases) | `Thread` objects, events, slot map, lane arrays. Stacks are native memory. One-time. |
| `new JobBatch<TJob>(capacity)` | exactly 3 arrays of `capacity × sizeof(TJob)` | 10 000 × 16-byte elements = 480 KB. Doubles only if `Add` exceeds capacity. |
| Warm-up frames | slot map and lane arrays double until they fit the working set | a handful of allocations in the first frames, then none |
| Delegate form `Schedule(ctx => ..., done => ...)` | 1 wrapper object per call (~40 B) plus the caller's delegates and closure (2–3 objects, once per closure scope) | event-rate only, by design |
| A struct passed to `Schedule(IFrameJob)` | one box per call | use `JobBatch<TJob>` for struct jobs |
| A job that throws | the exception, its stack trace and the log string — kilobytes | `LastFrameFaults` is the alarm; a job faulting every frame is a GC problem, not just a logging one |
| `Dispose` | a few objects (thread exit) | one-time |

Two things beyond allocation rate. First, **scanning**: Boehm is non-moving, and an array whose element type contains no references is allocated pointer-free and is never traversed by the collector — a `JobBatch` of plain-value structs costs the GC nothing however large it is. A batch whose elements carry a reference (an `Enemy`, a `Wall[]`) is scanned at every collection: three buffers × capacity × reference fields. At 10 000 elements that is tens of thousands of pointers, well under a millisecond, but it is the reason to prefer an index or id over a reference in a very large batch. Second, **pauses**: Boehm stops every managed thread to collect, workers included. A collection triggered by the main thread's garbage lands in the middle of a chunk, lengthens that frame's critical path, and can cause a skipped barrier. The scheduler never triggers a collection itself once warm; how often the workers get paused is decided by the game's own allocation rate.

---

## 6. Worked use cases

Hypothetical, but each one exists to show a rule from section 2 in practice. None of them reference game code.

### 6.1 Pathfinding requests for many agents — pooling, supersede, stale handles

Requests are pooled objects (rule 3); a new request for the same agent cancels the old one; the callback checks it is still the current request before touching the dictionary, which is why `FrameJobHandle` has equality.

```csharp
public sealed class PathRequest : IFrameJob, IFrameJobCallback
{
    private readonly PathfindingService _service;
    public PathRequest(PathfindingService service) => _service = service;

    public GridSnapshot   Grid;                          // in — immutable while any request holds it
    public Vector2Int     From, To;                      // in
    public Agent          Owner;                         // main-only; never touched in Execute
    public FrameJobHandle Handle;

    public readonly List<Vector2Int> Path = new(64);     // out
    private readonly NodeHeap _open   = new(256);        // scratch, owned, reused
    private readonly HashSet<int> _closed = new();

    public void Execute(in FrameContext ctx)
    {
        Path.Clear(); _open.Clear(); _closed.Clear();
        AStar.Solve(Grid, From, To, _open, _closed, Path);
    }

    public void OnComplete(bool cancelled) => _service.Finished(this, cancelled);
}

public sealed class PathfindingService
{
    private readonly IJobScheduler _scheduler;
    private readonly Stack<PathRequest> _pool = new();
    private readonly Dictionary<Agent, FrameJobHandle> _inFlight = new();

    public PathfindingService(IJobScheduler scheduler) => _scheduler = scheduler;

    public void RequestPath(Agent agent, Vector2Int to, GridSnapshot grid)
    {
        if (_inFlight.TryGetValue(agent, out FrameJobHandle previous)) _scheduler.Cancel(previous);

        PathRequest request = _pool.Count > 0 ? _pool.Pop() : new PathRequest(this);
        request.Grid = grid; request.From = agent.Cell; request.To = to; request.Owner = agent;
        request.Handle = _scheduler.Schedule(request);
        _inFlight[agent] = request.Handle;
    }

    internal void Finished(PathRequest request, bool cancelled)
    {
        if (!cancelled) request.Owner.FollowPath(request.Path);

        if (_inFlight.TryGetValue(request.Owner, out FrameJobHandle current) && current == request.Handle)
            _inFlight.Remove(request.Owner);              // a superseded request must not remove its successor's handle

        request.Owner = null;
        _pool.Push(request);                              // safe: OnComplete runs after any execution has finished
    }
}
```

The grid is the shared input. It must be immutable while requests are in flight: when a wall is placed, publish a new `GridSnapshot` and let in-flight requests finish against the old one (they hold a reference, so it stays alive), or cancel them all first. Mutating the live grid under a running request is the one way to get a real data race out of this system.

### 6.2 Crowd steering as a batch — freezing a shared input

Each element needs the *other* agents' positions. Copying every neighbour into every element is quadratic; sharing one array is a race unless the array is frozen for the frame. The fix is to double-buffer the snapshot yourself, and the producer rule (`PendingCount == 0`) is exactly the condition that makes one of the two buffers free.

```csharp
public sealed class NeighbourSnapshot
{
    public Vector3[] Positions = new Vector3[1024];
    public int       Count;
}

public struct FlockJob : IFrameJob
{
    public int               Index;
    public Vector3           Position, Target;   // in
    public NeighbourSnapshot Neighbours;         // in — frozen for the frame the element runs in
    public Vector3           Steering;           // out

    public void Execute(in FrameContext ctx)
    {
        Vector3 separation = Vector3.zero;
        for (int i = 0; i < Neighbours.Count; i++)
        {
            Vector3 away = Position - Neighbours.Positions[i];
            float   d2   = away.sqrMagnitude;
            if (d2 > 0f && d2 < 4f) separation += away / d2;
        }
        Steering = (Target - Position).normalized + separation;
    }
}

public sealed class Flock
{
    private readonly JobBatch<FlockJob>  _batch;
    private readonly NeighbourSnapshot[] _snapshots = { new(), new() };
    private readonly List<Boid>          _boids;
    private int _write;

    public Flock(IJobScheduler scheduler, List<Boid> boids)
    {
        _boids = boids;
        _batch = new JobBatch<FlockJob>(capacity: boids.Count);
        scheduler.Register(_batch);
    }

    public void Update(float dt)
    {
        foreach (ref readonly FlockJob r in _batch.Results) _boids[r.Index].Integrate(r.Steering, dt);

        if (_batch.PendingCount != 0) return;

        NeighbourSnapshot snapshot = _snapshots[_write];
        _write ^= 1;
        snapshot.Count = _boids.Count;
        for (int i = 0; i < _boids.Count; i++) snapshot.Positions[i] = _boids[i].Position;

        for (int i = 0; i < _boids.Count; i++)
        {
            ref FlockJob j = ref _batch.Add();
            j.Index = i; j.Position = _boids[i].Position; j.Target = _boids[i].Target;
            j.Neighbours = snapshot;
        }
    }
}
```

Why two snapshots are enough: when `PendingCount == 0` right after a barrier, the elements that reference snapshot A were just handed off and are executing; the elements that referenced snapshot B ran last frame and were collected at this same barrier. B is free, so we write B. If the barrier skipped, `PendingCount != 0`, nothing is written, and both snapshots stay untouched. The invariant is "write only the snapshot no executing or pending element references", and the producer rule enforces it for free.

### 6.3 A two-phase pipeline — integrate, then aggregate

Phase 0 is a batch that integrates particles and writes each result into `Shared[Index]`. Phase 1 is one reference job that reads the whole `Shared` array and computes the bounds; its result reaches the main thread through `OnComplete`. Because it is scheduled every frame, it is gated on the batch's hand-off, not on its own completion.

```csharp
public struct IntegrateJob : IFrameJob
{
    public int       Index;
    public Vector3   Position, Velocity;       // in/out
    public Vector3[] Shared;                   // out — this element writes only Shared[Index]

    private static readonly Vector3 Gravity = new(0f, -9.81f, 0f);   // not Physics.gravity: that is an engine call

    public void Execute(in FrameContext ctx)
    {
        Velocity += Gravity * ctx.DeltaTime;
        Position += Velocity * ctx.DeltaTime;
        Shared[Index] = Position;
    }
}

public sealed class BoundsJob : IFrameJob, IFrameJobCallback
{
    public Vector3[] Shared;
    public int       Count;
    public Bounds    Result;                   // worker writes, main reads in OnComplete only
    public Action<Bounds> Published;

    public void Execute(in FrameContext ctx)
    {
        Vector3 min = Shared[0], max = Shared[0];
        for (int i = 1; i < Count; i++) { min = Vector3.Min(min, Shared[i]); max = Vector3.Max(max, Shared[i]); }
        Result = new Bounds((min + max) * 0.5f, max - min);
    }

    public void OnComplete(bool cancelled) { if (!cancelled) Published(Result); }
}

// setup — PhaseCount must be 2
var scheduler = new JobScheduler(new JobSchedulerOptions { PhaseCount = 2 });
var shared    = new Vector3[count];                              // worker-only scratch; main never reads it
var batch     = new JobBatch<IntegrateJob>(count, phase: 0);
var bounds    = new BoundsJob { Shared = shared, Count = count, Published = b => _worldBounds = b };
scheduler.Register(batch);

// every frame
foreach (ref readonly IntegrateJob r in batch.Results) _particles[r.Index].Set(r.Position, r.Velocity);

if (batch.PendingCount == 0)
{
    for (int i = 0; i < count; i++)
    {
        ref IntegrateJob j = ref batch.Add();
        j.Index = i; j.Position = _particles[i].Position; j.Velocity = _particles[i].Velocity; j.Shared = shared;
    }
    scheduler.Schedule(bounds, phase: 1);                        // same object each frame; see below
}
```

Why the same `BoundsJob` object every frame is safe here: its previous run finished before the barrier that handed off this batch (otherwise the barrier would have skipped and `PendingCount` would not be 0), and its `OnComplete` ran inside that barrier before the workers were kicked. So at any moment the object is owned by exactly one side.

### 6.4 Procedural mesh bake — a heavy one-shot with owned buffers

```csharp
public sealed class ChunkMeshJob : IFrameJob, IFrameJobCallback
{
    public byte[] Voxels;                                    // in — given to the job; main leaves it alone until OnComplete
    public Mesh   Target;                                    // main-only

    public readonly List<Vector3> Vertices = new(4096);      // out, owned, reused
    public readonly List<int>     Indices  = new(8192);

    public void Execute(in FrameContext ctx)
    {
        Vertices.Clear(); Indices.Clear();
        GreedyMesher.Build(Voxels, Vertices, Indices);       // several milliseconds of pure C# — that is the point
    }

    public void OnComplete(bool cancelled)
    {
        if (cancelled) return;
        Target.Clear();
        Target.SetVertices(Vertices);
        Target.SetTriangles(Indices, 0);
        Target.RecalculateNormals();
    }
}
```

One thing to know before scheduling a job that takes longer than a frame: the executing set is frozen per frame, so one 30 ms job delays *every* other job's results by the frames it overruns (the barrier skips until it finishes). Nothing breaks, but a steering batch sharing the scheduler will feel it. Either split the bake into slices scheduled over several frames, or give heavy asynchronous work its own `JobScheduler` instance with one worker — nothing stops you from running two.

### 6.5 Search-as-you-type — event-rate work with supersede

```csharp
private FrameJobHandle _search;

void OnQueryChanged(string query)
{
    _scheduler.Cancel(_search);                              // stale or Invalid → false, harmless

    string[]     catalogue = _catalogue;                     // treated as immutable: replaced on change, never mutated
    List<string> hits      = new();
    _search = _scheduler.Schedule(
        ctx => { foreach (string s in catalogue) if (s.Contains(query, StringComparison.OrdinalIgnoreCase)) hits.Add(s); },
        cancelled => { if (!cancelled) _resultsView.Show(hits); });
}
```

The delegate form allocates per call, which is fine at keystroke rate. Because a superseded search is cancelled before its callback runs, the view only ever shows results for the latest query.

---

## 7. Anti-patterns

Each of these compiles and most of them even work in the editor, until they do not.

| Don't | Because |
|---|---|
| Read a repeating job's fields from the main thread | a worker writes them every frame; there is no main-owned moment. Publish via a batch or two alternating one-shots. |
| `Schedule` the same object while it is pending | two workers execute one object in the same frame and race on its fields. Gate on `IsPending` or on a batch's `PendingCount`. |
| `batch.Add` while `PendingCount > 0` | the skipped-barrier snowball (5.5). |
| Mutate a `List`/array a job in flight holds | the one real data race available. Hand the collection over, or publish an immutable replacement. |
| `Time.deltaTime`, `transform.position`, `Physics.gravity`, `Random.value`, `Debug.Log` per element inside `Execute` | engine calls off the main thread; some crash, some silently return garbage, `Debug.Log` locks and allocates. |
| `ListPool<T>.Rent` inside `Execute` | main-thread pool; editor logs an error, player corrupts the pool. |
| Call `Schedule`/`Cancel` from inside `Execute` or another thread | throws `InvalidOperationException`; the scheduler is single-writer. Return follow-up work as data and schedule it in `OnComplete`. |
| `WaitForIdle()` every frame | you have rebuilt the old blocking dispatcher. |
| Keep reading `Results` after the next barrier as if it were the same frame | it may have rotated; re-fetch the span each frame. |

---

## 8. How it works inside

For when you want the bottom-up picture. Nothing here is needed to use the system.

**The barrier** (`JobScheduler.Tick`, main thread, start of `EarlyUpdate`):
1. If a frame is in flight and its workers have not all finished → `SkippedFrames++`, return.
2. `Collect()` — for every executed one-shot, in schedule order, free its slot and call `OnComplete(false)`; cancelled slots get `OnComplete(true)`. Batches promote their executing buffer to `Results`.
3. `HandOff()` — per phase lane: swap pending ⇄ executing arrays, compact out jobs cancelled before hand-off, merge repeating jobs staged during the frame into the main-owned repeating array (ordered compaction on cancel), rotate every registered batch's three buffers.
4. `BuildChunks()` — cut every source (one-shot array, repeating array, each batch) into contiguous chunks, aiming for `ChunksPerWorker × WorkerCount` per source, never finer than `MinBatchChunkItems` for batches; sort by phase.
5. Publish the frozen `FrameContext`, reset the chunk cursor and phase counters, `Reset` the done-event, `Set` every worker's kick-event. Without workers, the barrier runs every chunk itself, in order, and sets the done-event.

**Workers** (`WorkerMain`): wait on a private kick-event, reset it, then loop `Interlocked.Increment(ref _nextChunk) - 1` to claim chunks. A chunk whose phase is not yet open spins briefly, then yields, until the last chunk of the previous phase opens it. Each chunk runs its slice with a per-element `try/catch` that logs and counts. The last worker to finish sets the done-event. Ownership transfer, not locking: the kick-event `Set`/`Wait` and done-event `Set`/`IsSet` pairs are the release/acquire fences that make the main thread's writes visible to workers and vice versa. The events are `ManualResetSignal`s, a volatile flag with a lock behind it, because `ManualResetEventSlim` allocates its lock the first time a thread blocks on it: a GC allocation in whichever frame first parks a worker.

**Job slots** live in a `SlotMap<JobSlot>` (main-thread-only, generational). `FrameJobHandle` wraps the `Handle<JobSlot>`; `Cancel` flips a flag in the slot and bumps a lane counter — the worker never looks at it. A stale handle fails the generation check.

**Batches** hold three `TJob[]` arrays. `Add` writes into pending; `Rotate` at the barrier makes pending the executing array, the finished executing array the results, and the old results the new pending (cleared lazily by `Add`). If the executing array was empty, results are left in place — the sticky-results rule.

**Hot loops** (`ReferenceJobRunner.ExecuteRange`, `JobBatch.ExecuteRange`) carry `[Il2CppSetOption(ArrayBoundsChecks/NullChecks, false)]`, which gives IL2CPP pointer-loop codegen without `unsafe` anywhere in the assembly. Boehm GC is non-moving, so pointer-free struct arrays are also never scanned.

**Dispose**: detach from the player loop; if a frame is in flight wait up to 2 s and collect it (warn and abandon it otherwise); set `_stopping`, kick every worker so it observes the flag and exits, join each with a 2 s timeout, never `Thread.Abort`; then deliver `OnComplete(true)` to every job that never ran, and unregister every batch. Double `Dispose` is a no-op; every other call afterwards throws `ObjectDisposedException`.

**Invariants the tests pin down** (`UGFW/Tests/EditMode/Jobs`, `UGFW/Tests/PlayMode/Jobs`): a job scheduled mid-frame is invisible to workers until the next barrier; completion order equals schedule order for 1, 2 and 4 workers, and without any; a throwing job kills nothing; a skipped barrier swaps nothing; a phase-1 job observes phase-0 output over 1000 frames with 4 workers; dispose mid-flight collects the frame, cancels the rest and joins the threads; steady state performs zero GC allocations on the main thread and on every worker (measured with the profiler's `GC.Alloc` recorder — Unity's Mono returns 0 from `GC.GetAllocatedBytesForCurrentThread` unconditionally, so byte deltas from it prove nothing; a count over every thread is the fewest of three runs, as the editor's own threads allocate now and then); the player-loop system is present while attached and gone after dispose.

---

## 9. Testing job code

Job logic is plain C#: unit-test `Execute` directly with a hand-built context — `job.Execute(new FrameContext(frame: 1, time: 0.5f, unscaledTime: 0.5f, deltaTime: 0.016f))` — no scheduler needed. For integration, a PlayMode test with an attached scheduler sees the real cadence (`UGFW/Tests/PlayMode/Jobs/JobSchedulerPlayModeTests.cs` is the template). Driving the barrier by hand (`Tick`) is currently `internal` to `AK.Jobs` and visible only to the UGFW test assemblies; see section 10.

---

## 10. Open decisions

Things the current implementation does not do, with the reason, so they can be argued about with the design in view.

1. **Time- and frame-deferred scheduling** (`Schedule(job, delayFrames)`, `Schedule(job, delaySeconds)`, `ScheduleRepeating(job, intervalSeconds, startDelay)`). Not present. The old dispatcher needed a third thread and a priority queue for these; in this design they are a main-thread filter at the barrier over a small deferred list, touching no worker code. Left out for scope, not for difficulty. If added, an interval job that misses its due time because of a skipped barrier should run once and advance past `now`, never burst to catch up.
2. **Per-frame callback for repeating jobs.** A repeating job has no main-owned moment, which is why reading its fields is forbidden. An `OnFrame()` delivered inside the barrier would create one. Cheap to add; the question is whether it encourages per-frame main-thread work that belongs in a batch anyway.
3. **`Tick` as public API.** Would let game test assemblies drive the barrier without PlayMode, and would allow a custom driver (fixed-step simulation, server tick). Kept internal until there is a second driver.
4. **A blocking barrier mode.** `WaitForIdle` at the top of `Tick` would guarantee every frame runs all jobs, at the cost of frame time. Deliberately not the default on a phone; could be an option for deterministic simulation.
5. **Default `WorkerCount`.** `clamp(cores − 2, 1, 4)` is a guess until the benchmark scene has run on the Android device. Editor numbers (i9-13900K, 10k-element spring batch at 60 fps): main-thread baseline 4.41 ms/frame → 1 worker 0.55 ms main / 4.05 ms critical, 2 workers 2.04 ms critical, 4 workers 1.06 ms critical.
6. **`unsafe` / `NativeArray` storage.** Rejected for now: `Il2CppSetOption` gives IL2CPP the same loop codegen, `NativeArray` bounds checks compile out in players anyway, and Boehm is non-moving. Revisit only with a device measurement that says otherwise.
