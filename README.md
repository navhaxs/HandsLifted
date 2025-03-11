# VisionScreens

VisionScreens is church presentation software, designed to be easy to use & flexible for modern day church needs.

Find out more at https://visionscreens.twohandslifted.com

> 👀 This is the 2025 development branch.
> 
> Under heavy & rapid development.

(c) Jeremy Wong 2015-2025

![Screenshot](screenshot-nov2023.jpg?raw=true "Screenshot")
![Screenshot](screenshot.jpg?raw=true "Screenshot")

## Dev set up

I have been testing using 'Syncfusion File Formats' library to import PPTX files on systems that do not have Microsoft PowerPoint installed, or non-Windows platforms.
(This process works by using the Syncfusion library to convert the PPTX to PDF, then using PDFiumCore/Ghostscript to convert the PDF to a set of PNG images).

To use this feature, please set an ENV variable `SyncfusionLicenseKey=<your license key>`. You can obtain a license key for free via registering at Syncfusion. They also offer a free license for open-source projects.

## Acknowledgements

Avalonia UI
* https://avaloniaui.net

LibMPV
* https://github.com/mpv-player/mpv
* https://gist.github.com/dbrookman/74b8bcfb37a23452f7137b83bca9580f
* https://github.com/homov/LibMpv

NewTek NDI SDK
* https://www.ndi.tv

Material Icons

Config.Net

Serilog

PDFiumCore