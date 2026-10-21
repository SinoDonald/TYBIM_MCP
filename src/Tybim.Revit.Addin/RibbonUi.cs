using System;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace Tybim.Revit.Addin
{
    internal static class RibbonUi
    {
        private const string TabName = "拓源數位";
        private const string PanelName = "MCP";
        // Internal ID is separate from the displayed name and other TYBIM buttons.
        private const string ButtonId = "TYBIM.MCP.ShowAiAssistant.A405238F";

        internal static void Register(UIControlledApplication app)
        {
            try { app.CreateRibbonTab(TabName); }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // TYBIM or another add-in may already own this shared tab.
                // GetRibbonPanels below still fails normally if the tab is absent.
            }
            var panel = app.GetRibbonPanels(TabName).FirstOrDefault(p => p.Name == PanelName)
                ?? app.CreateRibbonPanel(TabName, PanelName);
            if (!panel.GetItems().Any(item => item.Name == ButtonId))
            {
                var assembly = Assembly.GetExecutingAssembly();
                var data = new PushButtonData(ButtonId, "AI助手", assembly.Location, typeof(Chat.ShowChatCommand).FullName)
                {
                    // As in TYBIM_Button: use the supplied 32px image as LargeImage.
                    LargeImage = RibbonImages.LoadImage("Tybim.Mcp.Images.AiAssistant.png"),
                    ToolTip = "【功能介紹】\n在 Revit 內使用 Codex、Claude 或 Gemini 對話，透過 MCP 查詢專案、樓層與模型資訊，並預覽及確認執行已支援的模型操作。",
                    LongDescription = "【使用步驟】\n1. 開啟 Revit 專案，點選「AI助手」展開對話面板。\n2. 選擇 AI 引擎：Codex 使用 ChatGPT 登入；Claude／Gemini 輸入對應 API Key。\n3. 按「連線／取得模型」，選擇模型並輸入需求，例如查詢樓層或預覽放門。\n4. 修改模型前，核對預覽中的範圍、門型、位置與略過原因，再按執行確認。\n5. 完成後查看工具結果；需要時使用 Revit Undo 還原，並自行儲存模型。",
                    ToolTipImage = RibbonImages.LoadImage("Tybim.Mcp.Images.AiAssistant.Original.png", 250)
                };
                panel.AddItem(data);
            }
            const string cameraId = "TYBIM.MCP.Study.CameraManager";
            if (!panel.GetItems().Any(item => item.Name == cameraId))
            {
                var camera = new PushButtonData(cameraId, "相機管理", typeof(RevitMCP.Commands.CameraManagerCommand).Assembly.Location,
                    typeof(RevitMCP.Commands.CameraManagerCommand).FullName);
                camera.LargeImage = RibbonImages.LoadImage("Tybim.Mcp.Images.CameraManager.png");
                camera.ToolTipImage = RibbonImages.LoadImage("Tybim.Mcp.Images.CameraManager.Original.png", 250);
                camera.ToolTip = "【功能介紹】\n集中管理目前專案的相機視圖，查詢與調整相機高度、角度及位置，搭配位置圖、Key Plan 與視圖操作檢查成果。面板啟動時先隱藏，點選本按鈕即可開啟。";
                camera.LongDescription = "【使用步驟】\n1. 開啟 Revit 專案，點選「相機管理」展開面板。\n2. 按「重新整理」載入目前專案的相機清單。\n3. 選擇相機，檢查樓層、離樓板高度及方向；雙擊相機名稱可開啟對應視圖。\n4. 依需求使用位置圖、Key Plan 或視圖操作，修改後檢查模型與圖面結果。\n5. 使用完畢可關閉面板，需要時再點本按鈕開啟；模型變更可依操作使用 Revit Undo，並自行儲存。";
                panel.AddItem(camera);
            }
        }
    }

    internal static class RibbonImages
    {
        internal static BitmapSource LoadImage(string resource, int width = 0)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException("Missing ribbon image: " + resource))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                if (width > 0) image.DecodePixelWidth = width;
                image.StreamSource = stream;
                image.EndInit();
                // TYBIM's CreateBitmapSourceFromHBitmap produces a 96-DPI source.
                // PNGs can carry 300 DPI: 32 pixels then occupy only 10.24 WPF
                // units. Normalize the in-memory DPI without changing the file,
                // pixel size, or pixels. No GDI handle/dependency is needed.
                var pixelsSource = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
                int stride = pixelsSource.PixelWidth * 4;
                var pixels = new byte[stride * pixelsSource.PixelHeight];
                pixelsSource.CopyPixels(pixels, stride, 0);
                var source = BitmapSource.Create(pixelsSource.PixelWidth, pixelsSource.PixelHeight,
                    96, 96, PixelFormats.Bgra32, null, pixels, stride);
                source.Freeze();
                return source;
            }
        }
    }
}
