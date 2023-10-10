using System;

namespace Bolter
{
    public abstract class AbstractSecurity
    {
        protected abstract void _PrepareEnableSecurity();

        public bool IsSecurityActive { get { return isLocked; } }
        private bool isLocked = false;

        // Each abstract security has a startDate and en endDate, as a security, so that it will one day stop
        public DateTime startDate
        {
            get => _startDate; set
            {
                if (value > endDate) { throw new InvalidOperationException($"Start date ({startDate}) cannot be superior to end date ({endDate})"); }
                _startDate = value;
            }
        }
        public DateTime endDate
        {
            get => _endDate; set
            {
                if (value < startDate) { throw new InvalidOperationException($"End date ({endDate}) cannot be inferior to start date ({startDate})"); }
                _endDate = value;
            }
        } 
        
        private DateTime _startDate;
        private DateTime _endDate;

        public string TimeRemaining
        {
            get
            {
                if (startDate == null || endDate == null) return "Dates not set yet";
                TimeSpan timeRemaining = endDate - startDate;
                return Math.Round(timeRemaining.TotalHours, 1) + " H, " + Math.Round(timeRemaining.TotalMinutes, 2) + " Min, " + Math.Round(timeRemaining.TotalSeconds, 2) + " Sec";
            }
        }
        public string Kind
        {
            get
            {
                return this.GetType().Name;
            }
        }

        public AbstractSecurity(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
            {
                throw new InvalidOperationException($"We cannot have a start date {startDate.ToLongTimeString()} - {startDate.ToLongDateString()}" +
                    $" superior to an end date {endDate.ToLongTimeString()} - {endDate.ToLongDateString()}");
            }
            this.startDate = startDate;
            this.endDate = endDate;
        }

        public void PreparingEnableSecurity()
        {
            Console.WriteLine(">> Preparing Security " + Kind);
            _PrepareEnableSecurity();
            Console.Write(" DONE");
        }
        protected abstract void _EnableSecurity();

        public void EnableSecurity()
        {
            Console.WriteLine(">> Enabling Security " + Kind);
            Console.WriteLine(">> Details : " + ToString());
            try
            {
                _EnableSecurity();
                isLocked = true;
                Console.WriteLine(">> Enabled Security");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[BOLTER] Failed to Enable Security for " + Kind + ", rollbacking");
                _DisableSecurity();
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
        protected abstract void _DisableSecurity();

        public void DisableSecurity()
        {
            Console.WriteLine(">> Disabling Security " + Kind);
            Console.WriteLine(">> Details : " + ToString());
            try
            {
                _DisableSecurity();
                isLocked = false;
                Console.WriteLine(">> Disabled Security");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[BOLTER] Failed to Disable Security for " + Kind + ", Emergency unlock for all bolter");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}