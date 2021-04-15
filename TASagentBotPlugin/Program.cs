using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;

using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using streamdeck_client_csharp;

namespace TASagentBotPlugin
{
    class Program
    {
        public class Options
        {
            [Option("port", Required = true, HelpText = "The websocket port to connect to", SetName = "port")]
            public int Port { get; set; }

            [Option("pluginUUID", Required = true, HelpText = "The UUID of the plugin")]
            public string PluginUUID { get; set; }

            [Option("registerEvent", Required = true, HelpText = "The event triggered when the plugin is registered?")]
            public string RegisterEvent { get; set; }

            [Option("info", Required = true, HelpText = "Extra JSON launch data")]
            public string Info { get; set; }
        }

        // StreamDeck launches the plugin with these details
        // -port [number] -pluginUUID [GUID] -registerEvent [string?] -info [json]
        static void Main(string[] args)
        {
            // Uncomment this line of code to allow for debugging
            // while (!System.Diagnostics.Debugger.IsAttached) { System.Threading.Thread.Sleep(100); }

            // The command line args parser expects all args to use `--`, so, let's append
            for (int count = 0; count < args.Length; count++)
            {
                if (args[count].StartsWith("-") && !args[count].StartsWith("--"))
                {
                    args[count] = $"-{args[count]}";
                }
            }

            Parser parser = new Parser((with) =>
            {
                with.EnableDashDash = true;
                with.CaseInsensitiveEnumValues = true;
                with.CaseSensitive = false;
                with.IgnoreUnknownArguments = true;
                with.HelpWriter = Console.Error;
            });

            ParserResult<Options> options = parser.ParseArguments<Options>(args);
            options.WithParsed<Options>(o => RunPlugin(o));
        }

        private enum MicState
        {
            NoConnection = 0,
            Normal,
            Modified
        }
        private enum LockdownState
        {
            NoConnection = 0,
            Open,
            Locked
        }

        static JObject globalSettings = new JObject();
        static Image noConnectionMicImage = null;
        static Image normalMicImage = null;
        static Image moddedMicImage = null;
        static List<string> micTesters = new List<string>();
        static StreamDeckConnection connection;
        static MicState micState = MicState.NoConnection;


        static List<string> lockTesters = new List<string>();
        static Image noConnectionLockImage = null;
        static Image unlockedImage = null;
        static Image lockedImage = null;
        static LockdownState lockState = LockdownState.NoConnection;

        static Image CurrentMicImage
        {
            get
            {
                switch (micState)
                {
                    case MicState.NoConnection: return noConnectionMicImage;
                    case MicState.Normal: return normalMicImage;
                    case MicState.Modified: return moddedMicImage;
                    default: throw new Exception();
                }
            }
        }

        static Image CurrentLockImage
        {
            get
            {
                switch (lockState)
                {
                    case LockdownState.NoConnection: return noConnectionLockImage;
                    case LockdownState.Open: return unlockedImage;
                    case LockdownState.Locked: return lockedImage;
                    default: throw new Exception();
                }
            }
        }

        static bool ToggledLockState
        {
            get
            {
                switch (lockState)
                {
                    case LockdownState.NoConnection: return true;
                    case LockdownState.Open: return true;
                    case LockdownState.Locked: return false;
                    default: throw new Exception();
                }
            }
        }
        

        static void RunPlugin(Options options)
        {
            ManualResetEvent connectEvent = new ManualResetEvent(false);
            ManualResetEvent disconnectEvent = new ManualResetEvent(false);

            connection = new StreamDeckConnection(options.Port, options.PluginUUID, options.RegisterEvent);

            connection.OnConnected += (sender, args) =>
            {
                connectEvent.Set();
            };

            connection.OnDisconnected += (sender, args) =>
            {
                disconnectEvent.Set();
            };

            connection.OnApplicationDidLaunch += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"App Launch: {args.Event.Payload.Application}");
            };

            connection.OnApplicationDidTerminate += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"App Terminate: {args.Event.Payload.Application}");
            };

            Dictionary<string, JObject> settings = new Dictionary<string, JObject>();
            connection.OnWillAppear += (sender, args) =>
            {
                lock (settings)
                {
                    settings[args.Event.Context] = args.Event.Payload.Settings;

                    if (settings[args.Event.Context] == null)
                    {
                        settings[args.Event.Context] = new JObject();
                    }

                    switch (args.Event.Action)
                    {
                        case "wtf.tas.tasagentbot.vfx":
                            if (!settings[args.Event.Context].ContainsKey("voiceEffect") || string.IsNullOrEmpty(settings[args.Event.Context]["voiceEffect"].Value<string>()))
                            {
                                settings[args.Event.Context]["voiceEffect"] = JValue.CreateString("none");
                            }
                            break;

                        case "wtf.tas.tasagentbot.sfx":
                            if (!settings[args.Event.Context].ContainsKey("soundEffect") || string.IsNullOrEmpty(settings[args.Event.Context]["soundEffect"].Value<string>()))
                            {
                                settings[args.Event.Context]["soundEffect"] = JValue.CreateString("sephiroth");
                            }
                            break;

                        case "wtf.tas.tasagentbot.rest":
                            if (!settings[args.Event.Context].ContainsKey("endPoint") || string.IsNullOrEmpty(settings[args.Event.Context]["endPoint"].Value<string>()))
                            {
                                settings[args.Event.Context]["endPoint"] = JValue.CreateString("/TASagentBotAPI/SFX/Skip");
                            }

                            if (!settings[args.Event.Context].ContainsKey("jsonBody") || string.IsNullOrEmpty(settings[args.Event.Context]["jsonBody"].Value<string>()))
                            {
                                settings[args.Event.Context]["jsonBody"] = JValue.CreateString("{\n  \"effect\": \"None\"\n}");
                            }
                            break;

                        case "wtf.tas.tasagentbot.micmonitor":
                            try
                            {
                                connection.SetImageAsync(CurrentMicImage, args.Event.Context, SDKTarget.HardwareAndSoftware, null);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Mic SetImage Exception: {ex}");
                            }

                            lock (micTesters)
                            {
                                micTesters.Add(args.Event.Context);
                            }
                            break;

                        case "wtf.tas.tasagentbot.lockdownmonitor":
                            try
                            {
                                connection.SetImageAsync(CurrentLockImage, args.Event.Context, SDKTarget.HardwareAndSoftware, null);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Lock SetImage Exception: {ex}");
                            }

                            lock (lockTesters)
                            {
                                lockTesters.Add(args.Event.Context);
                            }
                            break;
                    }
                }
            };

            connection.OnDidReceiveSettings += (sender, args) =>
            {
                lock (settings)
                {
                    settings[args.Event.Context] = args.Event.Payload.Settings;

                    if (settings[args.Event.Context] == null)
                    {
                        settings[args.Event.Context] = new JObject();
                    }

                    switch (args.Event.Action)
                    {
                        case "wtf.tas.tasagentbot.vfx":
                            if (!settings[args.Event.Context].ContainsKey("voiceEffect") || string.IsNullOrEmpty(settings[args.Event.Context]["voiceEffect"].Value<string>()))
                            {
                                settings[args.Event.Context]["voiceEffect"] = JValue.CreateString("none");
                            }
                            break;

                        case "wtf.tas.tasagentbot.sfx":
                            if (!settings[args.Event.Context].ContainsKey("soundEffect") || string.IsNullOrEmpty(settings[args.Event.Context]["soundEffect"].Value<string>()))
                            {
                                settings[args.Event.Context]["soundEffect"] = JValue.CreateString("sephiroth");
                            }
                            break;

                        case "wtf.tas.tasagentbot.rest":
                            if (!settings[args.Event.Context].ContainsKey("endPoint") || string.IsNullOrEmpty(settings[args.Event.Context]["endPoint"].Value<string>()))
                            {
                                settings[args.Event.Context]["endPoint"] = JValue.CreateString("/TASagentBotAPI/SFX/Skip");
                            }

                            if (!settings[args.Event.Context].ContainsKey("jsonBody") || string.IsNullOrEmpty(settings[args.Event.Context]["jsonBody"].Value<string>()))
                            {
                                settings[args.Event.Context]["jsonBody"] = JValue.CreateString("{\n  \"effect\": \"None\"\n}");
                            }
                            break;
                    }
                }
            };

            connection.OnDidReceiveGlobalSettings += (sender, args) =>
            {
                globalSettings = args.Event.Payload.Settings;

                if (globalSettings == null)
                {
                    globalSettings = new JObject();
                }

                if (!globalSettings.ContainsKey("botURL"))
                {
                    globalSettings.Add("botURL", JValue.CreateString("http://localhost:5000"));
                }

                if (!globalSettings.ContainsKey("configFilePath"))
                {
                    globalSettings.Add("configFilePath", JValue.CreateString("C:\\Users\\tasag\\Documents\\TASagentBot\\Config\\Config.json"));
                }
            };

            connection.OnWillDisappear += (sender, args) =>
            {
                lock (settings)
                {
                    if (settings.ContainsKey(args.Event.Context))
                    {
                        settings.Remove(args.Event.Context);
                    }

                    if (micTesters.Contains(args.Event.Context))
                    {
                        micTesters.Remove(args.Event.Context);
                    }
                }
            };

            connection.OnKeyDown += async (sender, args) =>
            {
                string botURL = globalSettings["botURL"].Value<string>();
                string configFilePath = globalSettings["configFilePath"].Value<string>();

                if (string.IsNullOrEmpty(botURL) ||
                    string.IsNullOrEmpty(configFilePath))
                {
                    return;
                }

                botURL = botURL.Trim();

                if (botURL.EndsWith("/"))
                {
                    botURL = botURL.Substring(0, botURL.Length - 1);
                }

                BotConfiguration config = JsonConvert.DeserializeObject<BotConfiguration>(
                    System.IO.File.ReadAllText(configFilePath));

                if (string.IsNullOrEmpty(config.AuthConfiguration.Admin.AuthString))
                {
                    return;
                }

                RestRequest request = new RestRequest(Method.POST);
                request.AddHeader("Authorization", config.AuthConfiguration.Admin.AuthString);

                RestClient restClient;

                switch (args.Event.Action)
                {
                    case "wtf.tas.tasagentbot.vfx":
                        {
                            string voiceEffect = settings[args.Event.Context]["voiceEffect"].Value<string>();

                            if (string.IsNullOrEmpty(voiceEffect))
                            {
                                return;
                            }

                            restClient = new RestClient(botURL + "/TASagentBotAPI/Mic/Effect");
                            request.AddJsonBody(
                                new VoiceEffectRequest()
                                {
                                    Effect = voiceEffect
                                });
                        }
                        break;

                    case "wtf.tas.tasagentbot.sfx":
                        {
                            string soundEffect = settings[args.Event.Context]["soundEffect"].Value<string>();

                            if (string.IsNullOrEmpty(soundEffect))
                            {
                                return;
                            }

                            restClient = new RestClient(botURL + "/TASagentBotAPI/SFX/PlayImmediate");
                            request.AddJsonBody(
                                new SoundEffectRequest()
                                {
                                    Effect = soundEffect
                                });
                        }
                        break;

                    case "wtf.tas.tasagentbot.rest":
                        {
                            string bodyString = settings[args.Event.Context]["jsonBody"].Value<string>();
                            string endPoint = settings[args.Event.Context]["endPoint"].Value<string>();

                            if (string.IsNullOrEmpty(bodyString) || string.IsNullOrEmpty(endPoint))
                            {
                                return;
                            }

                            if (!endPoint.StartsWith("/"))
                            {
                                endPoint = "/" + endPoint;
                            }

                            try
                            {
                                JObject requestBody = JObject.Parse(bodyString);

                                restClient = new RestClient(botURL + endPoint);
                                request.AddParameter("application/json; charset=utf-8", requestBody, ParameterType.RequestBody);
                                //request.AddJsonBody(requestBody);
                            }
                            catch (JsonReaderException)
                            {
                                return;
                            }
                        }
                        break;

                    case "wtf.tas.tasagentbot.micmonitor":
                        {
                            restClient = new RestClient(botURL + "/TASagentBotAPI/Mic/Effect");
                            request.AddJsonBody(
                                new VoiceEffectRequest()
                                {
                                    Effect = "None"
                                });
                        }
                        break;

                    case "wtf.tas.tasagentbot.lockdownmonitor":
                        {
                            restClient = new RestClient(botURL + "/TASagentBotAPI/Auth/Lockdown");
                            request.AddJsonBody(
                                new LockdownStatus()
                                {
                                    Locked = ToggledLockState
                                });
                        }
                        break;


                    default:
                        return;
                }

                await restClient.ExecuteAsync(request);
            };

            // Start the connection
            connection.Run();

            noConnectionMicImage = Image.FromFile(@"Images\MicMonitorIcon.png");
            normalMicImage = Image.FromFile(@"Images\NormalMic.png");
            moddedMicImage = Image.FromFile(@"Images\ModdedMic.png");

            noConnectionLockImage = Image.FromFile(@"Images\UndeterminedLockIcon.png");
            unlockedImage = Image.FromFile(@"Images\UnlockedIcon.png");
            lockedImage = Image.FromFile(@"Images\LockedIcon.png");

            // Wait for up to 10 seconds to connect
            if (connectEvent.WaitOne(TimeSpan.FromSeconds(10)))
            {
                connection.GetGlobalSettingsAsync().Wait();

                // We connected, loop every three seconds until we disconnect
                while (!disconnectEvent.WaitOne(TimeSpan.FromMilliseconds(1000)))
                {
                    if (micTesters.Count > 0)
                    {
                        TestMicSettings();
                    }

                    if (lockTesters.Count > 0)
                    {
                        TestLockSettings();
                    }
                }
            }
        }

        private static async void TestMicSettings()
        {
            string botURL = globalSettings["botURL"].Value<string>();

            if (string.IsNullOrEmpty(botURL))
            {
                return;
            }

            botURL = botURL.Trim();

            if (botURL.EndsWith("/"))
            {
                botURL = botURL.Substring(0, botURL.Length - 1);
            }

            RestRequest request = new RestRequest(Method.GET);

            RestClient restClient = new RestClient(botURL + "/TASagentBotAPI/Mic/Effect");

            IRestResponse response = await restClient.ExecuteAsync(request);

            MicState newMicState = ExtractMicState(response);

            if (newMicState != micState)
            {
                micState = newMicState;

                foreach (string uuid in micTesters.ToList())
                {
                    try
                    {
                        await connection.SetImageAsync(CurrentMicImage, uuid, SDKTarget.HardwareAndSoftware, null);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Mic SetImage Exception: {ex}");
                    }
                }
            }
        }

        private static async void TestLockSettings()
        {
            string botURL = globalSettings["botURL"].Value<string>();

            if (string.IsNullOrEmpty(botURL))
            {
                return;
            }

            botURL = botURL.Trim();

            if (botURL.EndsWith("/"))
            {
                botURL = botURL.Substring(0, botURL.Length - 1);
            }

            RestRequest request = new RestRequest(Method.GET);

            RestClient restClient = new RestClient(botURL + "/TASagentBotAPI/Auth/Lockdown");

            IRestResponse response = await restClient.ExecuteAsync(request);

            LockdownState newLockdownState = ExtractLockdownState(response);

            if (newLockdownState != lockState)
            {
                lockState = newLockdownState;

                foreach (string uuid in lockTesters.ToList())
                {
                    try
                    {
                        await connection.SetImageAsync(CurrentLockImage, uuid, SDKTarget.HardwareAndSoftware, null);
                    }
                    catch(Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Lock SetImage Exception: {ex}");
                    }
                }
            }
        }

        private static MicState ExtractMicState(IRestResponse response)
        {
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return MicState.NoConnection;
            }

            VoiceEffectRequest voiceEffect = JsonConvert.DeserializeObject<VoiceEffectRequest>(response.Content);

            string voiceEffectString = voiceEffect.Effect?.Trim()?.ToLowerInvariant() ?? "none";

            if (voiceEffectString == "" || voiceEffectString == "none")
            {
                return MicState.Normal;
            }

            return MicState.Modified;
        }

        private static LockdownState ExtractLockdownState(IRestResponse response)
        {
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return LockdownState.NoConnection;
            }

            LockdownStatus lockStatus = JsonConvert.DeserializeObject<LockdownStatus>(response.Content);

            return lockStatus.Locked ? LockdownState.Locked : LockdownState.Open;
        }
    }

    class SoundEffectRequest
    {
        public string Effect { get; set; } = "";
    }

    class VoiceEffectRequest
    {
        public string Effect { get; set; } = "";
    }

    class LockdownStatus
    {
        public bool Locked { get; set; } = false;
    }
}
