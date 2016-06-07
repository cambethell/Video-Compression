CAMERON BETHELL - Video Compression

IN THIS FOLDER: 
the debug executable - this should work to display functionality
the nomad1 and nomad2 jpgs that i tested this program with. they are hardcoded to be loaded in initially.
the visual studio project for this assignment.
the compression and decompression classes.

Testing what's going on:
The first two frames of a "video" are shown. press compress, wait until it is finished, and then press decompress.
what you see now are the two frames that are decompressed and the 2nd frame is constructed from the first frame using
pixel differences and motion vectors.

HOW TO USE:
Load Image: will load the image for that side it is clicked on.
	It will resize to the size of the image, but the two images must be the same size to function properly.

Compress: pushing this will do the compression of the two images and save a file called "thefile.cam"
The P Range number up/down will increase or decrease the p range.

Decompress: will open a file called "thefile.cam" and decompress and display the two frames.