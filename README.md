# Unofficial-VDB-Loader-for-Unity
Basic VDB loader to load embergen vdb's into unity, paired with a basic small realtime volume renderer.
Press play and it will load and render the file (may take a bit)

# Roadmap
- Extract code that generates the VDB from mesh elsewhere
- Camera / render settings (settings VDB render resolution, auto check depthTexture ?)
- import settings (VDBLoader ?) (max resolution, compression ?, quantization ?)
- Make it work on main camera (for now it renders to RenderTextures)
- Make animation work again (some of my changes broke the way animation worked)
- Add "VDBRenderer" options like MeshRenderer (calculate lighting (each frame, light changes, from code), receive / cast shadows...
- Support for point clouds ? (apparently vdb supports multiple data representation including point clouds)
- make voxels not display a uniform cube (when really close, we see cubes boundaries, we could exp(length) to avoid it)
- Check if missing features from VDB (colors, lighting...)
- Optimize (VDBRendererSettings samples, noise...) common and per platform settings ?
- Fix cubes holes in the model
- Make it work for all platforms (android, ios, web?...)
- Make it work on URP HDRP and legacy
- Make it work up to unity 2021 (it should already)
- Rename a bit some kernels and add some comment
- Check that position, scale rotation work, if not make it so
- How multiple VDB work mixed together ?
- Check with transparency ?
- Implement projected shadows
- Multiple light into account ?
- Compatible with baking / reflection probes ?
- Show Import size on Asset importer
- Finish Asset Importer
- Compress asset
- Import options (compression, max resolution, toPowerOf2...)
  
## Disclaimer
- Still early alpha tech
- Uses a lot of memory
- Cannot load compressed files
- Has errors on import (trying to change the way import is done)
- It has cubic wholes in some models (don't know if it's from models or renderer) 

## History
This was initially developped by [PJBomb2](https://github.com/Pjbomb2/Unofficial-Basic-Embergen-VDB-Loader-for-Unity) as a Basic VDB loader to load embergen vdb's into unity, paired with a basic small realtime volume renderer.
<br/>
I was looking to some VDB implementations for unity and I thought this one was cool and easy to tweak to my needs. I then decided to make these modifications available to anyone who has the same needs as I do :) as the work to make it a more ready to go solution isn't that hard.
<br/>
I'm just starting the journey with this, anyone interested to contribute is more than welcome.
## Example Images

![](/VolumeImages/1.png)
![](/VolumeImages/2.png)
![](/VolumeImages/vdb.png)

# Contributors
- [PJBomb2](https://github.com/Pjbomb2/Unofficial-Basic-Embergen-VDB-Loader-for-Unity)
<br/>
Original author of the VDB Voxel implementation
- [Me z0rg](https://z0rg.dev)
<br/>
Maintainer, trying to make it a ready to go solution