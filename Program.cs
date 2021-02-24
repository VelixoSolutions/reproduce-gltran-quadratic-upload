using AcumaticaServiceReference;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace reproduce_quadratic_gl_upload
{
    class Program
    {
        // CHANGE THIS IF NEEDED!
        // -
        private const string AcumaticaUri = "https://test.velixo.com/2020R1";
        private const string Tenant = "Company";
        private const string Username = "user";
        private const string Password = "password";
        
        private const string GLTransactionScreenID = "GL301000";
        private const string HeaderView = "BatchModule";
        private const string DetailView = "GLTranModuleBatNbr";
        private const string Branch = "PRODWHOLE";
        private const string Ledger = "ACTUAL";
        private const string DebitAccount = "10100";
        private const string CreditAccount = "10200";
        private static readonly DateTime DateEntered = new DateTime(2020, 1, 1);
        private const string FinPeriodID = "202001";
        private static readonly IFormatProvider FormatProvider = CultureInfo.CreateSpecificCulture("en-US");

        static async Task<ScreenSoap> LoginAsync()
        {
            BasicHttpBinding binding = new BasicHttpBinding();

            binding.Name = "ScreenSoap";
            binding.AllowCookies = true;
            binding.MaxReceivedMessageSize = 2147483647;
            binding.SendTimeout = new TimeSpan(0, 5, 0);
            binding.ReceiveTimeout = new TimeSpan(0, 5, 0);

            EndpointAddress address = new EndpointAddress(AcumaticaUri + "/Soap/.asmx");
            if (address.Uri.Scheme == Uri.UriSchemeHttps)
            {
                binding.Security.Mode = BasicHttpSecurityMode.Transport;
            }

            var screen = new ScreenSoapClient(binding, address);

            await screen.LoginAsync(Username + "@" + Tenant, Password);
            await screen.SetLocaleNameAsync("en-US");
            
            return screen;
        }

        static void AddTransactionLine(List<Command> commands, string account)
        {
            commands.Add(new NewRow { ObjectName = DetailView });
            commands.Add(new Value { ObjectName = DetailView, FieldName = "BranchID", Value = Branch });
            commands.Add(new Value { ObjectName = DetailView, FieldName = "AccountID", Value = account });
            commands.Add(new Value { ObjectName = DetailView, FieldName = "ProjectID", Value = "X" });

            if (account == DebitAccount)
            {
                commands.Add(new Value { ObjectName = DetailView, FieldName = "CuryDebitAmt", Value = 100.ToString(FormatProvider), Commit = true });
            }
            else
            {
                commands.Add(new Value { ObjectName = DetailView, FieldName = "CuryCreditAmt", Value = 100.ToString(FormatProvider), Commit = true });
            }
        }

        static async Task UploadAsync(int numberOfDetails)
        {

            ScreenSoap screen = null;

            try
            {
                screen = await LoginAsync();

                var commands = new List<Command>();

                //Setup keys and add
                commands.Add(new Key { ObjectName = HeaderView, FieldName = "Module", Value = $"=[{HeaderView}.Module]" });
                commands.Add(new Key { ObjectName = HeaderView, FieldName = "BatchNbr", Value = $"=[{HeaderView}.BatchNbr]" });
                commands.Add(new AcumaticaServiceReference.Action { ObjectName = HeaderView, FieldName = "Cancel" });
                commands.Add(new Value { ObjectName = HeaderView, FieldName = "BatchNbr", Value = "<NEW>", Commit = true });

                //Header
                commands.Add(new Value { ObjectName = HeaderView, FieldName = "Hold", Value = false.ToString(FormatProvider) });
                commands.Add(new Value { ObjectName = HeaderView, FieldName = "BranchID", Value = Branch });
                commands.Add(new Value { ObjectName = HeaderView, FieldName = "LedgerID", Value = Ledger });
                commands.Add(new Value { ObjectName = HeaderView, FieldName = "DateEntered", Value = DateEntered.ToString(FormatProvider) });
                commands.Add(new Value { ObjectName = HeaderView, FieldName = "FinPeriodID", Value = FinPeriodID });
                commands.Add(new Value { ObjectName = HeaderView, FieldName = "AutoReverse", Value = false.ToString(FormatProvider) });

                for (int index = 0; index < numberOfDetails / 2; ++index)
                {
                    AddTransactionLine(commands, DebitAccount);
                    AddTransactionLine(commands, CreditAccount);
                }

                //We want the RefNbr to be returned
                commands.Add(new Field { ObjectName = HeaderView, FieldName = "BatchNbr" });
                commands.Add(new AcumaticaServiceReference.Action { ObjectName = HeaderView, FieldName = "Save" });

                var results = await screen.SubmitAsync(GLTransactionScreenID, commands.ToArray());
            }
            finally
            {
                if (screen != null)
                {
                    await screen.LogoutAsync();
                }
            }
        }

        static async Task Main(string[] args)
        {
            int[] transactionNumbers = new[] { 2, 500, 1000, 1500, 2000, 2500 };

            foreach (var count in transactionNumbers)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                await UploadAsync(count);

                stopwatch.Stop();

                Console.WriteLine($"Uploaded {count} in {stopwatch.ElapsedMilliseconds / 1000.0} seconds");
                Console.Out.Flush();
            }
        }
    }
}
