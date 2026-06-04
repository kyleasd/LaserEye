from PIL import Image

data = """
....HHHHHHHH....
...HHHHHHHHHH...
..HSSSSSSSSSSH..
..HSSSSSSSSSSH..
.HSSSaSSSaSSSH..
.HSaRRRaRRRaSH..
.HSRWWWRWWWRSH..
.HSaRRRaRRRaSH..
.HSSSaSSSaSSSH..
..HSSSSSSSSSH...
..HSSSOOOSSSH...
...HSSSSSSSH....
...BBBGGGBBB....
..BBBBBBBBBBB...
..BBBBGGGBBBB...
..BBBBBBBBBBB...
"""

colors = {
    '.': (0, 0, 0, 0),
    'H': (240, 200, 80, 255),   # hair
    'S': (255, 210, 170, 255),  # skin
    'O': (160, 110, 80, 255),   # mouth/shadow
    'W': (255, 255, 255, 255),  # laser core
    'R': (255, 30, 30, 255),    # laser main
    'a': (180, 0, 0, 255),      # laser aura
    'B': (30, 60, 140, 255),    # suit
    'G': (210, 170, 40, 255),   # gold
}

img = Image.new('RGBA', (16, 16))
pixels = img.load()

lines = [line.strip() for line in data.strip().split('\n')]
for y, line in enumerate(lines):
    for x, char in enumerate(line):
        pixels[x, y] = colors[char]

img.save("assets/BuffsIcons.png")
