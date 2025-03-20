using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.Extensions.Hosting;
using System.Diagnostics.Eventing.Reader;
using System.Web;

namespace smart_local
{
    /// <summary>
    /// Main Program 
    /// </summary>
    public static class Program
    {

        private const string _defaultFhirServerUrl = "https://launch.smarthealthit.org/v/r4/sim/WzIsIiIsIjI2NjA2MjQiLCJBVVRPIiwwLDAsMCwiIiwiIiwiIiwiIiwiIiwiIiwiIiwwLDEsIiJd/fhir";
        
        private static string _authCode = string.Empty;
        private static string _clientState = string.Empty;
        /// <summary>
        /// program to access a SMART FHIR server with a local webserver for redirection  
        /// </summary>
        /// <param name="fhirServerUrl">FHIR R4 endpoint URL</param>
        /// <returns></returns>
        static int Main(string fhirServerUrl)
        {
            if (string.IsNullOrEmpty(fhirServerUrl))
            {
                fhirServerUrl = _defaultFhirServerUrl;
            }
            System.Console.WriteLine($"  FHIR Server: {fhirServerUrl}");

            Hl7.Fhir.Rest.FhirClient fhirClient = new Hl7.Fhir.Rest.FhirClient(fhirServerUrl);

            if (!FhirUtils.TryGetSmartUrls(fhirClient, out string authorizeUrl, out string tokenUrl)){
                System.Console.WriteLine("failed to discover smart urls");
                return -1;
            }
            else
            {
                System.Console.WriteLine($"Authorize URL: {authorizeUrl}");
                System.Console.WriteLine($"    Token URL: {tokenUrl}");

                 Task.Run(() => CreateHostBuilder().Build().Run())  ;
                //CreateHostBuilder().Build().Start();

                int listenPort = GetListenPort().Result; //often it creates dead blocks use below ones
                 //int listenPort = GetListenPort().GetAwaiter().GetResult();
                 System.Console.WriteLine($" Listening on : {listenPort}");

                 //here we are building a string url based on url (see in https://www.hl7.org/fhir/smart-app-launch/app-launch.html)

                 //example to build string url to authonticate
                    // Location: https://ehr/authorize?
                    // response_type=code&
                    // client_id=app-client-id& // if we are doing this on real fhir server we are going to need have a pre registred APP Id , since we are using lanch right now we dont really care 
                    // redirect_uri=https%3A%2F%2Fapp%2Fafter-auth&
                    // launch=xyz123&
                    // scope=launch+patient%2FObservation.rs+patient%2FPatient.rs+openid+fhirUser&
                    // state=98wrghuwuogerg97&
                    // aud=https://ehr/fhir

                string url =
                     $"{authorizeUrl}"+
                     $"?response_type=code"+
                     $"&client_id=fhir_demo_id" +
                     $"&redirect_uri={HttpUtility.UrlEncode($"http://127.0.0.1:{listenPort}")}"+
                     $"&scope={HttpUtility.UrlEncode($"openid fhirUser profile launch/patient patient/*.read")}"+
                     $"&state=local_state"+
                     $"&aud={fhirServerUrl}";
                LaunchUrl(url);

               // http://127.0.0.1:49838/?error=invalid_request&error_description=Bad+audience+value+%22https%3A%2F%2Flaunch.smarthealthit.org%2Fv%2Fr4%2Fsim%2FWzIsIiIsIjI2NjA2MjQiLCJBVVRPIiwwLDAsMCwiIiwiIiwiIiwiIiwiIiwiIiwiIiwwLDEsIiJd%2Ffhir%2F%22.+Expected+%22https%3A%2F%2Flaunch.smarthealthit.org%2Fv%2Fr4%2Fsim%2FWzIsIiIsIjI2NjA2MjQiLCJBVVRPIiwwLDAsMCwiIiwiIiwiIiwiIiwiIiwiIiwiIiwwLDEsIiJd%2Ffhir%22.&state=local_state
                //https://launch.smarthealthit.org/select-patient?response_type=code&client_id=fhir_demo_id&redirect_uri=http%3A%2F%2F127.0.0.1%3A49926&scope=openid+fhirUser+profile+launch%2Fpatient+patient%2F*.read&state=local_state&aud=https%3A%2F%2Flaunch.smarthealthit.org%2Fv%2Fr4%2Fsim%2FWzIsIiIsIjI2NjA2MjQiLCJBVVRPIiwwLDAsMCwiIiwiIiwiIiwiIiwiIiwiIiwiIiwwLDEsIiJd%2Ffhir
               //http://127.0.0.1:50118/?code=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJjb250ZXh0Ijp7Im5lZWRfcGF0aWVudF9iYW5uZXIiOnRydWUsInNtYXJ0X3N0eWxlX3VybCI6Imh0dHBzOi8vbGF1bmNoLnNtYXJ0aGVhbHRoaXQub3JnL3NtYXJ0LXN0eWxlLmpzb24iLCJwYXRpZW50IjoiNWVlMDUzNTktNTdiZi00Y2VlLThlODktOTEzODJjMDdlMTYyIn0sImNsaWVudF9pZCI6ImZoaXJfZGVtb19pZCIsInJlZGlyZWN0X3VyaSI6Imh0dHA6Ly8xMjcuMC4wLjE6NTAxMTgiLCJzY29wZSI6Im9wZW5pZCBmaGlyVXNlciBwcm9maWxlIGxhdW5jaC9wYXRpZW50IHBhdGllbnQvKi5yZWFkIiwicGtjZSI6ImF1dG8iLCJjbGllbnRfdHlwZSI6InB1YmxpYyIsInVzZXIiOiJQcmFjdGl0aW9uZXIvMjY2MDYyNCIsImlhdCI6MTc0MjM5NTQyNCwiZXhwIjoxNzQyMzk1NzI0fQ.6rdzJiYdZ1c9ftGVC8g5dSecQytKwcrcrMj4Bd7wPrk&state=local_state
                // LaunchUrl("http://github.com");
                 for (int loops = 0; loops < 30; loops++)
                 {
                    System.Threading.Thread.Sleep(1000);
                 }
            
            }
            return 0;
        }
        /// <summary>
        /// set the authorization code and state 
        /// </summary>
        /// <param name="code"></param>
        /// <param name="state"></param>
        public static void SetAuthCode(string code , string state)
        {
            _authCode = code ;
            _clientState = state;

            System.Console.WriteLine($"Code recieved : {code}");
        }
        /// <summary>
        /// Launch a URL in the user's default web browser .
        /// </summary>
        /// <param name="url"></param>
        /// <returns>true if successfull , false otherwise</returns>

        public static bool LaunchUrl(string url)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = url,
                    UseShellExecute = true,
                };

                Process.Start(startInfo);
                return true;
            }
            catch (Exception)
            {
                System.Console.WriteLine("Failed to launch URL");
                //return false;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    url = url.Replace("&" , "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}"){CreateNoWindow = true});
                    return true;
                }
                catch (Exception)
                {
                   //ignore
                }
                
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string [] allowedProgramsToRun = {"xdg-open", "gnome-open" ,"kfmclient"};

                foreach (string helper in allowedProgramsToRun)
                {
                    try
                    {
                        Process.Start(helper,url);
                        return true;
                    }
                    catch (Exception)
                    {
                      //ignore
                    }
                }
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    Process.Start("open", url);
                    return true;
                }
                catch (Exception)
                {
                    //ignore
                }

            }
            System.Console.WriteLine($"Failed to Launch URL");
            return false;
        }
        /// <summary>
        /// Determine the listening of the web server
        /// </summary>
        /// <returns></returns>
        public static async Task<int> GetListenPort(){
            // int listenPort =0;

            // while (listenPort ==0)
            //await Task.Delay(500);
            for(int loops =0 ; loops < 100 ; loops++)
            {
                await Task.Delay(500);
                if(Startup.Addresses == null){
                    continue;
                }
                string address = Startup.Addresses.Addresses.FirstOrDefault();

                if(string.IsNullOrEmpty(address)){
                    continue;
                }

                if(address.Length <18){
                    continue;
                }
                if ((int.TryParse(address.Substring(17), out int port))&& (port !=0))
                {
                    return port;
                }
            }
            //return 0;
            throw new Exception ($"Failed to get Listen port !");
        }
        public static IHostBuilder CreateHostBuilder() =>
        Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseUrls("http://127.0.0.1:0");
            webBuilder.UseKestrel();
            webBuilder.UseStartup<Startup>();
        });
    }
}
