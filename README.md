# Unofficial-Basic-Embergen-VDB-Loader-for-Unity
# NOT OFFICIAL FROM EMBERGEN; just a utility I made to load them into unity and uploaded in case anyone else finds it useful
## If you like what I do and want to support me and this project(as this takes a LOT of my time), consider becoming a Github Sponsor!
</br>
Basic VDB loader to load embergen vdb's into unity, paired with a basic small realtime volume renderer
</br>
To load a single file, just drag it into the object field in the script of the MainCamera component
</br>
To load multiple files to be animated, drag a folder with vdb's in it into the object field
</br>
Press play and it will load and render the files(may take a bit)
</br>
Uses a lot of memory
</br>
Cannot load compressed files

EDIT from seb776
- Made it a unity project else the project seems broken (broken script reference)
- Originally made to work on builtin renderpipeline, this version runs in URP (even if only on a rendertexture for now)
- Want to make it a ready to use solution => it would be really cool :)


## Example Images

![](/VolumeImages/1.png)
![](/VolumeImages/2.png)

