import easyocr
import sys
import json

# Ensure output is UTF‑8 encoded
sys.stdout.reconfigure(encoding='utf-8')

if len(sys.argv) != 2:
    print('[]')
    sys.exit(0)

img = sys.argv[1]

# Create EasyOCR reader that supports Chinese (simplified) and English
reader = easyocr.Reader(['ch_sim', 'en'])

results = reader.readtext(img, detail=1)

json_list = []
for r in results:
    json_list.append({
        'text': r[1],
        'conf': float(r[2]),
        'x': int(r[0][0][0]),
        'y': int(r[0][0][1])
    })

print(json.dumps(json_list, ensure_ascii=False))