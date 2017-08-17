namespace Wexflow.Core
{
    public class TaskStatus
    {
        public Status Status { get; set; }
        /// <summary>
        /// If and While condition
        /// </summary>
        public bool Condition { get; set; }

        /// <summary>
        /// Switch value
        /// </summary>
        public string SwitchValue { get; set; }

        public TaskStatus(Status status)
        {
            Status = status;
        }

        public TaskStatus(Status status, bool condition)
            : this(status)
        {
            Condition = condition;
        }

        public TaskStatus(Status status, string switchValue)
            : this(status)
        {
            SwitchValue = switchValue;
        }

        public TaskStatus(Status status, bool condition, string switchValue)
            : this(status)
        {
            Condition = condition;
            SwitchValue = switchValue;
        }
    }
}