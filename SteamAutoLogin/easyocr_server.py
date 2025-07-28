import easyocr
import sys
import json

# 确保输出为 UTF-8 编码
sys.stdout.reconfigure(encoding='utf-8')

# 确保传递的参数正确
if len(sys.argv) != 2:
    print('[]')
    sys.exit(0)

# 获取图像路径
img = sys.argv[1]

# 创建 EasyOCR Reader 实例，支持中文和英文
reader = easyocr.Reader(['ch_sim', 'en'])

# 读取图片并进行 OCR 识别
results = reader.readtext(img, detail=1)

# 存储 OCR 结果
json_list = []
for r in results:
    json_list.append({
        'text': r[1],  # 识别的文本
        'conf': float(r[2]),  # 置信度
        'x': int(r[0][0][0]),  # x 坐标
        'y': int(r[0][0][1])   # y 坐标
    })

# 输出 OCR 结果，确保 UTF-8 编码
print(json.dumps(json_list, ensure_ascii=False))
