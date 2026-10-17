from PIL import Image, ImageOps, ImageFilter, ImageEnhance

src = Image.open(r"F:\AI\NeuroGames\GTA5\work\footage\art\radio_2.png").convert("RGB")
w, h = src.size
# правая часть: студия с диджеем
crop = src.crop((int(w * 0.40), 0, w, h))
# вертикальный wizard 430x824
wiz = ImageOps.fit(crop, (430, 824), method=Image.LANCZOS, centering=(0.62, 0.30))
wiz.save(r"D:\Projects\TEMP\gta5-radio\installer\art\wizard_bg.png")
# фон установщика
back = ImageOps.fit(crop, (1272, 922), method=Image.LANCZOS, centering=(0.6, 0.4))
back = back.filter(ImageFilter.GaussianBlur(6))
back = ImageEnhance.Brightness(back).enhance(0.30)
back = ImageEnhance.Color(back).enhance(0.8)
back.save(r"D:\Projects\TEMP\gta5-radio\installer\art\back_radio.png")
print("wizard_bg", wiz.size, "back", back.size)
