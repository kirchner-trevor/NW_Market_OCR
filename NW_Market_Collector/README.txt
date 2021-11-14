Thank you for downloading NW Market Collector!

= Getting started =

To get started, simply run NW_Market_Collector.exe, and paste in the credentials you were given.
You can then optionally provider a user to help with troubleshooting.

That's it! It will also save this information into the config.json file to use the next time.

= How does it work? =

NW Market Collector will look for the game window, and if it finds it, will see if the market is open.
If it finds the market it will start uploading screenshots of the market listings after doing some 
pre-processing.

The pre-processing consists of cropping the image, converting it to back and white, removing some background
noise, and doing a quick-pass of text extraction on it to see if we've seen that screenshot before.

= F.A.Q. =

Q: NW Market Collector says it crashed, what should I do?
A: Reach out to us, and provide us with the log.txt file.

Q: What platforms does this run on?
A: Windows only, as it uses Windows specific functions to find the game.

Q: What resolutions does this run on?
A: It was designed for 1920x1080, but any 16:9 aspect ratio should work out of the box. Other resolutions may need to setup a custom
   Market Area.

Q: Why is the collector status is stuck at "Looking for New World"?
A: Any time the game window loses focus the collector pauses. If it is still stuck when the game has focus reach out for support.

Q: Why does Latest Text Blob never have a value?
A: If Latest Text Blob never has a value it means the text extraction isn't working, and you may need to setup a custom Market Area.

Q: How do I setup a custom Market Area?
A: Open the market in game and take a screenshot. Then open that screenshot in paint and first crop it to only the game window. Then record
   the coordinates of the market area by mousing over the top left and bottom right of the listings area, starting just to the left of the       name of the first item, and ending just below the location of the last item. Open the config.json file with a text editor and put the top
   left coordinates in for the X and Y values, then the distance between the left and right for the Width, and the distance between the
   top and bottom for the Height.