using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using HandyControl.Data;
using Microsoft.Extensions.Configuration;
using Shadowsocks.Controller;
using Shadowsocks.Model;
using Shadowsocks.Util;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace BlsNeter
{
    public class MainWindowViewModel : ObservableObject
    {
        private bool _loaded;
        public bool Loaded
        {
            get => _loaded;
            set => SetProperty(ref _loaded, value);
        }

        private bool _connected;
        public bool Connected
        {
            get => _connected;
            set => SetProperty(ref _connected, value);
        }

        private bool _globalModeChecked;
        public bool GlobalModeChecked
        {
            get => _globalModeChecked;
            set => SetProperty(ref _globalModeChecked, value);
        }

        private bool _pacModeChecked;
        public bool PacModeChecked
        {
            get => _pacModeChecked;
            set => SetProperty(ref _pacModeChecked, value);
        }

        private string _connectedStatusText;
        public string ConnectedStatusText
        {
            get => _connectedStatusText;
            set => SetProperty(ref _connectedStatusText, value);
        }

        private ObservableCollection<Server> servers;
        public ObservableCollection<Server> Servers
        {
            get { return servers; }
            set
            {
                SetProperty(ref servers, value);
            }
        }

        private Server currentServer;
        public Server CurrentServer
        {
            get { return currentServer; }
            set
            {
                if (value == currentServer)
                    return;

                SetProperty(ref currentServer, value);
                ToggleServer();
            }
        }

        private readonly Ping _pingSender = new Ping();
        public AsyncRelayCommand LoadCommandAsync { get; set; }
        public AsyncRelayCommand RefreshDelayCommandAsync { get; set; }
        public RelayCommand SwitchProxyStatusCommand { get; set; }
        public RelayCommand GlobalCheckedCommand { get; set; }
        public RelayCommand PacCheckedCommand { get; set; }
        public RelayCommand ExitApplicationCommand { get; set; }
        public RelayCommand OpenGitAddressCommand { get; set; }
        private readonly IConfiguration _configuration;

        public MainWindowViewModel(IConfiguration configuration)
        {
            LoadCommandAsync = new AsyncRelayCommand(LoadAsync);
            RefreshDelayCommandAsync = new AsyncRelayCommand(RefreshDelayCommand);
            SwitchProxyStatusCommand = new RelayCommand(Proxy);
            GlobalCheckedCommand = new RelayCommand(UseGlobalMode);
            PacCheckedCommand = new RelayCommand(UsePacMode);
            ExitApplicationCommand = new RelayCommand(Exit);
            OpenGitAddressCommand = new RelayCommand(OpenGitAddress);
            _configuration = configuration;
            GlobalModeChecked = true;
            PacModeChecked = false;
            ConnectedStatusText = "科学上网";
        }

        private async Task LoadAsync()
        {
            Loaded = false;
            Global.LoadConfig();
            Global.Controller = new MainController();
            Utils.SetTls();
            while (true)
            {
                try
                {
                    await InitalizeAsync();
                    break;
                }
                catch
                {
                    await Task.Delay(2000);
                }
            }

            Servers = new ObservableCollection<Server>(Global.GuiConfig.Configs);
            if (Servers != null && Servers.Count > 0)
            {
                CurrentServer = Servers.Count > Global.GuiConfig.Index ? Servers[Global.GuiConfig.Index] : Servers.FirstOrDefault();
                RefreshDelayCommand();
            }
            else
            {
                Growl.Warning(new GrowlInfo()
                {
                    WaitTime = 2,
                    Message = "无法找到合适的服务器!",
                    ShowDateTime = false,
                });
            }
            Loaded = true;
        }

        private void OpenGitAddress() 
        {
            string url = "http://github.com/BruceQiu1996/WPFScienceSurfing";
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.UseShellExecute = false;    //不使用shell启动
                p.StartInfo.RedirectStandardInput = true;//喊cmd接受标准输入
                p.StartInfo.RedirectStandardOutput = false;//不想听cmd讲话所以不要他输出
                p.StartInfo.RedirectStandardError = true;//重定向标准错误输出
                p.StartInfo.CreateNoWindow = true;//不显示窗口
                p.Start();//向cmd窗口发送输入信息 后面的&exit告诉cmd运行好之后就退出
                p.StandardInput.WriteLine("start " + url + "&exit");
                p.StandardInput.AutoFlush = true;
                p.WaitForExit();
                p.Close();
            }
        }

        private async Task RefreshDelayCommand()
        {
            if (Servers == null)
                return;
            await Task.Run(async () =>
            {
                foreach (var server in Servers)
                {
                    var result = await _pingSender.SendPingAsync(server.server, 5000);
                    if (result.Status == IPStatus.Success)
                    {
                        server.Delay = result.RoundtripTime.ToString() + "ms";
                    }
                    else
                    {
                        server.Delay = "超时";
                    }
                }
            });
        }

        private void Proxy()
        {
            try
            {
                if (Connected)
                {
                    Global.Controller.Stop();
                    ConnectedStatusText = "科学上网";
                    Connected = false;
                }
                else
                {
                    Global.Controller.Reload();
                    ConnectedStatusText = "遨游结束";
                    Connected = true;
                }
            }
            catch (Exception ex)
            {
                Growl.Warning(new GrowlInfo()
                {
                    WaitTime = 2,
                    Message = $"代理失败：{ex}",
                    ShowDateTime = false,
                });
            }
        }

        private void Exit() 
        {
            Global.Controller?.Stop();
            Global.Controller = null;
            Environment.Exit(0);
        }

        private void UseGlobalMode()
        {
            Global.Controller.ToggleMode(Shadowsocks.Enums.ProxyMode.Global);
        }

        private void UsePacMode()
        {
            Global.Controller.ToggleMode(Shadowsocks.Enums.ProxyMode.Pac);
        }

        public void ToggleServer()
        {
            Global.Controller.DisconnectAllConnections(true);
            var index = Servers.IndexOf(CurrentServer);
            Global.Controller.SelectServerIndex(index);
        }

        private async Task InitalizeAsync()
        {
            using (var client = new HttpClient())
            {
                var content = await client.GetStringAsync(_configuration.GetSection("SsrUrl").Value);
                content = Encoding.Default.GetString(Convert.FromBase64String(content));
                var ssrs = content.Split('\n').ToList().Where(x => x.StartsWith("ssr"));
                var result = Global.Controller.AddServerBySsUrl(content);
                if (!result)
                    throw new Exception("无法打开订阅信息信息");
            }
        }
    }
}
