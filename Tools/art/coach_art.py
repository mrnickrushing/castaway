#!/usr/bin/env python3
"""Tutorial art, into Assets/Resources/UI/Coach:

- hand.png: the pointing hand (Font Awesome 6 Free Solid "hand-pointer", CC BY 4.0, https://fontawesome.com), white
  with a dark outline and a soft shadow.
- castaway.jpg: the castaway's portrait for the coach bubble, cut from the game's own key art (UI/Story/keyart.jpg).

    python3 -I Tools/art/coach_art.py
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "Coach")
FONT = os.environ.get("FA_SOLID", "/usr/share/fonts/truetype/font-awesome/fa-6.7.2-solid-900.ttf")


def hand():
    N, SS = 256, 4                      # output size, supersampling
    big = N * SS
    font = ImageFont.truetype(FONT, int(big * 0.70))
    mask = Image.new("L", (big, big), 0)
    ImageDraw.Draw(mask).text((big // 2, big // 2), chr(0xF25A), font=font, fill=255, anchor="mm")
    outline = mask.filter(ImageFilter.MaxFilter(1 + 2 * int(big * 0.022) | 1))
    shadow = outline.filter(ImageFilter.GaussianBlur(big * 0.03))
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    sh = Image.new("RGBA", (big, big), (0, 0, 0, 0)); sh.putalpha(shadow.point(lambda v: v * 0.55))
    img.alpha_composite(sh, (int(big * 0.03), int(big * 0.045)))
    rim = Image.new("RGBA", (big, big), (30, 22, 12, 0)); rim.putalpha(outline); img.alpha_composite(rim)
    grad = Image.new("RGBA", (big, big))
    for y in range(big):
        t = y / big
        ImageDraw.Draw(grad).line([(0, y), (big, y)], fill=(255, int(252 - 14 * t), int(244 - 34 * t), 255))
    grad.putalpha(mask); img.alpha_composite(grad)
    img.resize((N, N), Image.LANCZOS).save(os.path.join(OUT, "hand.png"))


def portrait():
    art = Image.open(os.path.join(ROOT, "Assets", "Resources", "UI", "Story", "keyart.jpg")).convert("RGB")
    art.crop((250, 200, 630, 580)).resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, "castaway.jpg"), quality=90)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    hand(); portrait()
    print("wrote", os.path.normpath(OUT))
