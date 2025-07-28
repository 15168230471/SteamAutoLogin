using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.Threading;

public static class EasyOcrHelper
{
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, UIntPtr dwExtraInfo);

    private const int MOUSEEVENTF_LEFTDOWN = 0x02;
    private const int MOUSEEVENTF_LEFTUP = 0x04;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    // 截图指定窗口并保存，同时返回窗口左上角
    public static string CaptureWindow(IntPtr hWnd, out int winLeft, out int winTop)
    {
        Debug.WriteLine($"[EasyOCR] 开始截图窗口句柄: {hWnd}");
        GetWindowRect(hWnd, out RECT rect);
        winLeft = rect.Left;
        winTop = rect.Top;
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        Debug.WriteLine($"[EasyOCR] 截图区域: Left={rect.Left}, Top={rect.Top}, Right={rect.Right}, Bottom={rect.Bottom}, Size={width}x{height}");
        Bitmap bmp = new Bitmap(width, height);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
        }
        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"ocr_temp_{DateTime.Now:HHmmss}.png");
        bmp.Save(filePath);
        Debug.WriteLine($"[EasyOCR] 已保存窗口截图: {filePath}");
        return filePath;
    }

    // 调用Python EasyOCR识别图片，返回json数组
    public static JArray RunEasyOcr(string imagePath)
    {
        string pythonExe = @"C:\Users\hp\AppData\Local\Programs\Python\Python311\python.exe"; // 建议使用环境变量或配置
        string scriptPath = "easyocr_server.py"; // 确保在exe同目录，或者用绝对路径

        Debug.WriteLine($"[EasyOCR] 开始调用Python OCR，图片: {imagePath}");
        ProcessStartInfo psi = new ProcessStartInfo(pythonExe, $"\"{scriptPath}\" \"{imagePath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        using (Process proc = Process.Start(psi))
        {
            string output = proc.StandardOutput.ReadToEnd();
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            Debug.WriteLine($"[EasyOCR] Python OCR返回: {output}");
            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.WriteLine($"[EasyOCR] Python 错误输出: {error}");
            }
            try
            {
                return JArray.Parse(output);
            }
            catch
            {
                MessageBox.Show("OCR解析失败：" + output);
                Debug.WriteLine($"[EasyOCR] OCR解析异常：{output}");
                return null;
            }
        }
    }

    // 查找关键字按钮并点击，详细日志
    public static bool AutoClickButton(IntPtr hWnd, string[] keywords, int offsetX = 0, int offsetY = 0)
    {
        int winLeft, winTop;
        string imgPath = CaptureWindow(hWnd, out winLeft, out winTop);
        var results = RunEasyOcr(imgPath);
        if (results == null)
        {
            MessageBox.Show("OCR未返回结果。");
            Debug.WriteLine("[EasyOCR] 未获得OCR结果。");
            return false;
        }

        Debug.WriteLine($"[EasyOCR] OCR返回 {results.Count} 个文本块：");
        foreach (var obj in results)
        {
            string text = obj["text"]?.ToString() ?? "";
            double conf = obj["conf"]?.Value<double>() ?? 0;
            int x = obj["x"]?.Value<int>() ?? 0;
            int y = obj["y"]?.Value<int>() ?? 0;
            Debug.WriteLine($"    识别: {text} @({x},{y}), 置信度: {conf}");
        }

        foreach (var obj in results)
        {
            string text = obj["text"]?.ToString() ?? "";
            int x = obj["x"]?.Value<int>() ?? 0;
            int y = obj["y"]?.Value<int>() ?? 0;

            foreach (string kw in keywords)
            {
                if (text.Contains(kw) && x > 0 && y > 0)
                {
                    Debug.WriteLine($"[EasyOCR] 命中关键字“{kw}”，点击点: ({x},{y})");
                    SetForegroundWindow(hWnd);
                    Thread.Sleep(100);
                    Cursor.Position = new Point(winLeft + x + offsetX, winTop + y + offsetY);
                    Thread.Sleep(80);
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                    Debug.WriteLine($"[EasyOCR] 已点击按钮: {text} ({x},{y}) [全局坐标:({winLeft + x},{winTop + y})]");
                    return true;
                }
            }
        }
        Debug.WriteLine("[EasyOCR] 未找到任何关键字按钮！");
        return false;
    }

    // 循环检测并点击关键按钮，支持超时设置
    public static bool WaitAndClickButton(IntPtr hWnd, string[] keywords, int maxRetry = 30, int intervalMs = 500)
    {
        Debug.WriteLine("[EasyOCR] 开始循环等待检测关键按钮...");
        for (int retry = 0; retry < maxRetry; retry++)
        {
            Debug.WriteLine($"[EasyOCR] OCR自动点击第{retry + 1}次...");
            if (AutoClickButton(hWnd, keywords))
            {
                Debug.WriteLine("[EasyOCR] 成功识别并点击按钮，退出等待循环。");
                return true;
            }
            Thread.Sleep(intervalMs);
        }
        Debug.WriteLine("[EasyOCR] 等待超时，未检测到关键按钮。");
        MessageBox.Show("未能在设定时间内识别到指定按钮！");
        return false;
    }
}
