using System;

namespace Riftline
{
    // A command-only acceptance controller. The caller sends the returned command through the real Game API.
    public sealed class RunAutopilot
    {
        private int commandedEvent = -1;
        private float nextDirectionTime;
        public int PreferredRoute { get; set; } = -1;
        public int CommandsIssued { get; private set; }

        public GameCommand NextCommand(RunSimulation run)
        {
            if (run == null || run.Phase != RunPhase.Running) return GameCommand.None;
            TrackEvent context = null;
            TrackEvent next = null;
            foreach (TrackEvent item in run.Events)
            {
                if (item.Passed || !run.IsEventActive(item)) continue;
                double ahead = RunSimulation.TimeAtDistance(item.Distance) - run.Elapsed;
                if (ahead < -.001d) continue;
                if ((item.Kind == ObstacleKind.Fork || item.Kind == ObstacleKind.TurnLeft || item.Kind == ObstacleKind.TurnRight)
                    && item.Distance - run.Distance <= run.Speed * 2f)
                {
                    if (context == null || item.Distance < context.Distance) context = item;
                }
                if (next == null || item.Distance < next.Distance) next = item;
            }
            if (context != null)
            {
                if (context.Resolved) return GameCommand.None;
                int direction = context.Kind == ObstacleKind.Fork ? (PreferredRoute < 0 ? -1 : 1) : context.Branch;
                return Issue(direction < 0 ? GameCommand.Left : GameCommand.Right, run, context.Id);
            }
            if (next == null) return GameCommand.None;
            double seconds = RunSimulation.TimeAtDistance(next.Distance) - run.Elapsed;
            if (seconds > 2.2d) return GameCommand.None;
            int targetLane = next.SafeLane;
            if (next.Kind == ObstacleKind.Chip || next.Kind == ObstacleKind.Archive) targetLane = next.Lane;
            if (run.Lane != targetLane && run.Elapsed >= nextDirectionTime)
                return Issue(run.Lane < targetLane ? GameCommand.Right : GameCommand.Left, run, next.Id);
            if (commandedEvent == next.Id) return GameCommand.None;
            if (next.RequiredAction == GameCommand.Jump && seconds <= .29d && seconds > .1d)
                return Issue(GameCommand.Jump, run, next.Id);
            if (next.RequiredAction == GameCommand.Slide && seconds <= .28d && seconds > .05d)
                return Issue(GameCommand.Slide, run, next.Id);
            if (next.RequiredAction == GameCommand.Phase && run.PhaseEnergy >= 1f && seconds <= .3d && seconds > .05d)
                return Issue(GameCommand.Phase, run, next.Id);
            return GameCommand.None;
        }

        private GameCommand Issue(GameCommand command, RunSimulation run, int eventId)
        {
            if (command == GameCommand.Left || command == GameCommand.Right) nextDirectionTime = run.Elapsed + .34f;
            else commandedEvent = eventId;
            CommandsIssued++;
            return command;
        }
    }
}
