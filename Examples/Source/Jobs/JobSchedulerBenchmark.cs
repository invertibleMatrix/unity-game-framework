using System;
using System.Diagnostics;
using System.Text;
using AK.Jobs;
using UnityEngine;

namespace AK.Examples.Jobs
{
	/// <summary>
	/// Drop on any GameObject and press Play. Runs the same per-frame simulation load on the main
	/// thread and then through a <see cref="JobScheduler"/> with each worker count in
	/// <see cref="WorkerCounts"/>, and logs a table: main-thread time spent on the load, frame time,
	/// the workers' critical path, and skipped frames. Read it from the Console or logcat; the same
	/// text is drawn on screen so a device run needs no cable.
	///
	/// The load is a damped spring integrated for <see cref="Substeps"/> steps per element — pure
	/// arithmetic on the element's own fields, which is exactly what a batch is for.
	/// </summary>
	public sealed class JobSchedulerBenchmark : MonoBehaviour
	{
		public struct SpringJob : IJob
		{
			public float Position;
			public float Velocity;
			public float Target;
			public int   Substeps;

			public void Execute(in FrameContext ctx)
			{
				float dt = 1f / 60f / Substeps;
				for (int i = 0; i < Substeps; i++)
				{
					float accel = (Target - Position) * 40f - Velocity * 4f;
					Velocity += accel * dt;
					Position += Velocity * dt;
				}
			}
		}

		[Tooltip("Elements simulated per frame.")]
		public int Elements = 10_000;

		[Tooltip("Integration steps per element per frame; scales the cost of one element.")]
		public int Substeps = 32;

		[Tooltip("0 = main thread baseline; otherwise the scheduler's worker count.")]
		public int[] WorkerCounts = { 0, 1, 2, 4 };

		public int FramesPerMode = 240;
		public int WarmupFrames  = 30;

		private struct ModeResult
		{
			public int    Workers;
			public double MainMs;
			public double FrameMs;
			public double CriticalMs;
			public int    Skipped;
		}

		private JobScheduler        _scheduler;
		private JobBatch<SpringJob> _batch;
		private SpringJob[]         _mainThreadElements;

		private int          _modeIndex = -1;
		private int          _frameInMode;
		private double       _mainTicks, _frameSeconds, _criticalTicks;
		private int          _samples;
		private ModeResult[] _results;
		private string       _report = "starting…";
		private int          _previousTargetFrameRate;

		private void Start()
		{
			_previousTargetFrameRate    = Application.targetFrameRate;
			Application.targetFrameRate = 60;

			_results            = new ModeResult[WorkerCounts.Length];
			_mainThreadElements = new SpringJob[Elements];
			NextMode();
		}

		private void OnDestroy()
		{
			_scheduler?.Dispose();
			Application.targetFrameRate = _previousTargetFrameRate;
		}

		private void Update()
		{
			if (_modeIndex >= WorkerCounts.Length) return;

			int  workers = WorkerCounts[_modeIndex];
			long t0      = Stopwatch.GetTimestamp();

			if (workers == 0)
			{
				var ctx = new FrameContext(Time.frameCount, Time.time, Time.unscaledTime, Time.deltaTime);
				for (int i = 0; i < _mainThreadElements.Length; i++)
				{
					_mainThreadElements[i].Target   = Mathf.Sin(i + Time.frameCount * 0.01f);
					_mainThreadElements[i].Substeps = Substeps;
					_mainThreadElements[i].Execute(in ctx);
				}
			}
			else if (_batch.PendingCount == 0)
			{
				// Last frame's elements were handed off; a producer that added while they were still
				// pending (barrier skipped) would only pile up work for the next hand-off.
				Span<SpringJob> results = _batch.Results;
				for (int i = 0; i < Elements; i++)
				{
					ref SpringJob job = ref _batch.Add();
					if (i < results.Length)
					{
						job.Position = results[i].Position;
						job.Velocity = results[i].Velocity;
					}

					job.Target   = Mathf.Sin(i + Time.frameCount * 0.01f);
					job.Substeps = Substeps;
				}
			}

			long mainTicks = Stopwatch.GetTimestamp() - t0;

			_frameInMode++;
			if (_frameInMode > WarmupFrames)
			{
				_mainTicks    += mainTicks;
				_frameSeconds += Time.unscaledDeltaTime;
				if (_scheduler != null) _criticalTicks += _scheduler.Stats.LastFrameCriticalTicks;
				_samples++;
			}

			if (_frameInMode >= FramesPerMode) FinishMode();
		}

		private void NextMode()
		{
			_modeIndex++;
			_frameInMode   = 0;
			_mainTicks     = 0;
			_frameSeconds  = 0;
			_criticalTicks = 0;
			_samples       = 0;

			if (_modeIndex >= WorkerCounts.Length)
			{
				_report = BuildReport();
				UnityEngine.Debug.Log("[JobSchedulerBenchmark]\n" + _report);
				return;
			}

			int workers = WorkerCounts[_modeIndex];
			if (workers > 0)
			{
				_scheduler = new JobScheduler(new JobSchedulerOptions { WorkerCount = workers });
				_scheduler.AttachToPlayerLoop();
				_batch = new JobBatch<SpringJob>(Elements);
				_scheduler.Register(_batch);
			}

			_report = $"mode {_modeIndex + 1}/{WorkerCounts.Length}: {(workers == 0 ? "main thread" : workers + " worker(s)")}, {Elements} elements × {Substeps} substeps";
		}

		private void FinishMode()
		{
			int    workers = WorkerCounts[_modeIndex];
			double perMs   = 1000.0 / Stopwatch.Frequency;

			_results[_modeIndex] = new ModeResult
			{
				Workers    = workers,
				MainMs     = _mainTicks * perMs / _samples,
				FrameMs    = _frameSeconds * 1000.0 / _samples,
				CriticalMs = _criticalTicks * perMs / _samples,
				Skipped    = _scheduler?.Stats.SkippedFrames ?? 0,
			};

			if (_scheduler != null)
			{
				_scheduler.Dispose();
				_scheduler = null;
				_batch     = null;
			}

			NextMode();
		}

		private string BuildReport()
		{
			var sb = new StringBuilder();
			sb.AppendLine($"{Elements} elements × {Substeps} substeps, {FramesPerMode - WarmupFrames} measured frames per mode, {SystemInfo.processorType} ({SystemInfo.processorCount} cores)");
			sb.AppendLine("workers | main-thread ms | frame ms | worker critical ms | skipped");

			foreach (ModeResult r in _results)
			{
				sb.AppendLine($"{(r.Workers == 0 ? "main" : r.Workers.ToString()),7} | {r.MainMs,14:0.000} | {r.FrameMs,8:0.00} | {r.CriticalMs,18:0.000} | {r.Skipped,7}");
			}

			return sb.ToString();
		}

		private void OnGUI()
		{
			GUI.Label(new Rect(16, 16, Screen.width - 32, Screen.height - 32), _report, new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true });
		}
	}
}
