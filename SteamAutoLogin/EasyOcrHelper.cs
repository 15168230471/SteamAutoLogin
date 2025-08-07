using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public static class EasyOcrHelper
{
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public static string CaptureWindow(IntPtr hWnd, out int winLeft, out int winTop)
    {
        // 截图并保存，返回文件路径
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

    public static JArray RunEasyOcr(string imagePath)
    {
        // 调用Python EasyOCR进行OCR识别并返回结果
        string pythonExe = @"C:\Users\hp\AppData\Local\Programs\Python\Python311\python.exe";
        string scriptPath = "easyocr_server.py";

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

    public static bool DetectKeyword(IntPtr hWnd, string[] keywords)
    {
        // 截图并识别
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

        // 检查是否找到目标字
        foreach (var obj in results)
        {
            string text = obj["text"]?.ToString() ?? "";
            foreach (string kw in keywords)
            {
                if (text.Contains(kw))
                {
                    Debug.WriteLine($"[EasyOCR] 找到目标字“{kw}”。");
                    return true; // 找到目标字，返回 true
                }
            }
        }

        Debug.WriteLine("[EasyOCR] 未找到目标字！");
        return false; // 未找到目标字，返回 false
    }

    public static bool WaitAndDetectKeyword(IntPtr hWnd, string[] keywords, int maxRetry = 30, int intervalMs = 500)
    {
        Debug.WriteLine("[EasyOCR] 开始循环等待检测目标字...");
        for (int retry = 0; retry < maxRetry; retry++)
        {
            Debug.WriteLine($"[EasyOCR] OCR自动检测第{retry + 1}次...");
            if (DetectKeyword(hWnd, keywords))
            {
                Debug.WriteLine("[EasyOCR] 成功识别到目标字，退出等待循环。");
                return true;
            }
            Thread.Sleep(intervalMs);
        }
        Debug.WriteLine("[EasyOCR] 等待超时，未检测到目标字。");
        MessageBox.Show("未能在设定时间内识别到目标字！");
        return false;
    }
}
