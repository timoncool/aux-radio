from PIL import Image, ImageOps, ImageFilter, ImageEnhance

src = Image.open(r"F:\AI\NeuroGames\GTA5\work\footage\art\radio_1.png").convert("RGB")
fit = ImageOps.fit(src, (1272, 922), method=Image.LANCZOS, centering=(0.5, 0.35))
blur = fit.filter(ImageFilter.GaussianBlur(6))
blur = ImageEnhance.Brightness(blur).enhance(0.32)
blur = ImageEnhance.Color(blur).enhance(0.8)
blur.save(r"D:\Projects\TEMP\gta5-radio\installer\art\back_radio.png")
print("back ok", blur.size)
src.save(r"D:\Projects\TEMP\gta5-radio\installer\art\radio_1.png")
print("copied", src.size)
