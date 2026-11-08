from PIL import Image,ImageOps
from pathlib import Path
import numpy as np
p=Path('AllMatters_docs/jigsaw-visual-detail/assets')
im=Image.open(p/'confirm_button.png').convert('RGBA');a=Image.new('RGBA',(300,132));a.paste(im.crop((0,0,150,132)),(0,0));a.paste(ImageOps.mirror(im.crop((0,0,150,132))),(150,0));a.save(p/'confirm_button-symmetric.png')
im=Image.open(p/'popup_vertical.png').convert('RGBA');a=np.asarray(im).astype(float)/255;m=np.array([[.2989,.587,.114],[.5959,-.2774,-.3216],[.2115,-.5229,.3114]]);h=(1-.505)*6.283185;c=np.cos(h);s=np.sin(h);shift=np.array([[1,0,0],[0,c,s],[0,-s,c]]);rgb=a[:,:,:3]@m.T@shift*.75@np.linalg.inv(m).T;a[:,:,:3]=np.clip(rgb,0,1);Image.fromarray((a*255).astype('uint8')).save(p/'popup_vertical-native-color.png')
