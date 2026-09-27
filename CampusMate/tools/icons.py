from PIL import Image,ImageDraw
from pathlib import Path
for name,color in [('CampusMate','#5550D9'),('PocketBudget','#167363')]:
 im=Image.new('RGB',(1024,1024),color);d=ImageDraw.Draw(im)
 if name=='CampusMate':
  d.rounded_rectangle((215,250,498,733),radius=38,fill='#FFFFFF')
  d.rounded_rectangle((526,250,809,733),radius=38,fill='#DFFFAB')
  for x in [272,583]:
   for y in [348,425,502]:d.rounded_rectangle((x,y,x+156,y+20),radius=8,fill=color)
 else:
  d.rounded_rectangle((205,291,820,740),radius=70,fill='#F1FFF3')
  d.rounded_rectangle((207,248,747,360),radius=44,fill='#B9E7BA')
  d.rounded_rectangle((605,425,866,601),radius=40,fill='#264E42')
  d.ellipse((661,480,715,534),fill='#C9F3B8')
 root=Path(__file__).resolve().parents[2]/name/'assets'
 for size in [192,512,1024]:im.resize((size,size),Image.Resampling.LANCZOS).save(root/f'icon-{size}.png')
