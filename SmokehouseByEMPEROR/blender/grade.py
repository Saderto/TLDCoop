"""Smoked looks from game textures: gradient map by luminance blended with original detail."""
import numpy as np, sys, os
from PIL import Image, ImageFilter
G='/mnt/project-files/smokehouse/game_models/'; O='/mnt/project-files/smokehouse/tex/'
RAMPS={
 'meat_hot': [(0.10,0.04,0.02),(0.40,0.165,0.065),(0.70,0.42,0.19)],
 'meat_cold':[(0.035,0.007,0.008),(0.16,0.028,0.03),(0.34,0.11,0.09)],
 'fish_hot': [(0.08,0.04,0.015),(0.42,0.24,0.08),(0.74,0.55,0.30)],
 'fish_cold':[(0.07,0.025,0.008),(0.36,0.14,0.035),(0.66,0.36,0.14)],
}
def grade(src,kind,keep=0.15,gamma=1.0,speck=0.0,seed=1,crust=False,hip=97):
    im=np.asarray(Image.open(G+src+'.png').convert('RGB')).astype(np.float32)/255
    lum=im@np.array([0.3,0.59,0.11])
    lo,hi=np.percentile(lum[lum>0.03],[3,hip]); t=np.clip((lum-lo)/(hi-lo),0,1)**gamma
    r=np.array(RAMPS[kind]); a=np.clip(t*2,0,1)[...,None]; b=np.clip(t*2-1,0,1)[...,None]
    g=r[0]*(1-a)+r[1]*a; g=g*(1-b)+r[2]*b
    # keep some original chroma detail
    chroma=im-lum[...,None]
    out=g+chroma*keep
    if speck:
        rng=np.random.default_rng(seed); n=rng.random(lum.shape); n=np.asarray(Image.fromarray((n*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32)/255
        out*= (1-speck*np.clip((n-0.5)*6,0,1))[...,None]
    if crust:
        rng=np.random.default_rng(seed+5); H,W=lum.shape
        for col,cnt,rad in(((0.03,0.02,0.02),1500,1.2),((0.74,0.70,0.64),320,0.9),((0.30,0.20,0.10),500,1.1)):
            m=np.zeros((H,W),np.float32); ys=rng.integers(0,H,cnt); xs=rng.integers(0,W,cnt); m[ys,xs]=1
            m=np.asarray(Image.fromarray((m*255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(rad*0.5))).astype(np.float32)/255
            m=np.clip(m*1.3,0,1)[...,None]; out=out*(1-m)+np.array(col)*m
    out=np.clip(out,0,1)**(1/1.0)
    return Image.fromarray((out*255).astype(np.uint8))
JOBS=[('GEAR_MeatCougar_Dif','meat',1.0),('GEAR_MeatPtarmigan_Dif','meat',1.0),('GEAR_MeatWolf_Dif','meat',1.0),('GEAR_MeatBear_Dif','meat',1.0),('GEAR_MeatMoose_Dif','meat',1.0),('GEAR_MeatVenison_Dif','meat',1.0),('GEAR_MeatRabbit_Dif','meat',1.0),('GEAR_CookedRainbowTrout_Dif','fish',0.8),('GEAR_CookedCohoSalmon_Dif','fish',0.8),
      ('GEAR_CookedLakeWhiteFish_Dif','fish',0.8),('GEAR_CookedSmallMouthBass_Dif','fish',0.8),('GEAR_CookedLakeBurbot_Dif','fish',0.8),('GEAR_CookedCopperFish_Dif','fish',0.8),
      ('GEAR_CookedLakeGoldeye_Dif','fish',0.8),('GEAR_CookedRedIrishLord_Dif','fish',0.8)]
for src,k,gm in JOBS:
    for mode in('hot','cold'):
        grade(src,f'{k}_{mode}',hip=(55 if ('Cougar' in src and mode=='cold') else 97),gamma=gm*((0.55 if 'Cougar' in src else 1.5) if mode=='cold' else (1.0 if k=='meat' else 1.25)),crust=(k=='meat' and mode=='cold'),speck=0.25 if mode=='cold' else 0.12).save(O+src.replace('_Dif','')+f'_{mode}.png')
print('ok')
