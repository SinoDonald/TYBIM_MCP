using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tybim.Revit.Upstream
{
    /// <summary>Branding for TYBIM-owned windows; never changes Revit or other add-ins.</summary>
    public static class TybimWindow
    {
        public static ImageSource CreateIcon()
        {
            using(var stream=typeof(TybimWindow).Assembly.GetManifestResourceStream("Tybim.Mcp.TYBIM.ico"))
            {
                if(stream==null) throw new InvalidOperationException("Missing TYBIM.ico resource.");
                var icon=BitmapFrame.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
                icon.Freeze();return icon;
            }
        }
        public static void ShowMessage(string title,string text,bool modal=false)
        {
            var window=new Window {Title=title,Icon=CreateIcon(),Width=590,SizeToContent=SizeToContent.Height,
                MaxHeight=720,ResizeMode=ResizeMode.CanMinimize,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            var panel=new StackPanel {Margin=new Thickness(20)};
            panel.Children.Add(new ScrollViewer {MaxHeight=560,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                Content=new TextBlock {Text=text,TextWrapping=TextWrapping.Wrap,FontSize=14}});
            var close=new Button {Content="關閉",Width=90,Margin=new Thickness(0,18,0,0),HorizontalAlignment=HorizontalAlignment.Right,IsDefault=true,IsCancel=modal};
            close.Click+=(sender,args)=>window.Close();panel.Children.Add(close);window.Content=panel;
            var handle=Process.GetCurrentProcess().MainWindowHandle;
            if(handle!=IntPtr.Zero)new WindowInteropHelper(window).Owner=handle;
            else window.WindowStartupLocation=WindowStartupLocation.CenterScreen;
            if(modal)window.ShowDialog();else window.Show();
        }
    }
}
