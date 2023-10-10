using System;
using System.Collections.Generic;

namespace Bolter
{
    // TODO singleton? 
    public class SecurityManager
    {
        private readonly IList<AbstractSecurity> securities;

        public SecurityManager(List<AbstractSecurity> securities)
        {
            this.securities = securities ?? new List<AbstractSecurity>();
        }

        public SecurityManager()
        {
            this.securities = new List<AbstractSecurity>();
        }

        public void AddSecurity(AbstractSecurity security)
        {
            this.securities.Add(security);
        }

        public void EnableSecurities()
        {
            Console.WriteLine(">> Enabling Securities");
            try
            {
                foreach(AbstractSecurity security in securities)
                {
                    security.EnableSecurity();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine(">> Securities will be completely disabled");
                DisableSecurities();
                NonAdmin.DisableAllNonAdminRestrictions();
            }
        }
        public void DisableSecurities()
        {
            var fail = false;
            foreach (AbstractSecurity security in securities)
            {
                try
                {
                    security.DisableSecurity();
                    Console.WriteLine();
                }
                catch (System.Exception e)
                {
                    fail = true;
                    Console.WriteLine(e.ToString());
                    Console.WriteLine("Failed to disable security : " + security.Kind);
                    Console.WriteLine(">> All external securities will be completely disabled");
                }
            }
            if(fail)
            {
                NonAdmin.DisableAllNonAdminRestrictions();
            }

        }
    }
}
