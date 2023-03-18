# DogeNodes

![Splash](https://user-images.githubusercontent.com/124823644/217800333-62e54878-27ab-474c-abf4-fa7cf2817073.png)

## Description

A Windows Forms based application to analyse the Dogecoin blockchain network and monitor the status of an individual node

## Screenshots

![Summary](https://user-images.githubusercontent.com/124823644/221171087-c6b806ef-80ff-4cc2-9aa1-63bef11a8807.png)

![Statistics](https://user-images.githubusercontent.com/124823644/221171110-f54ad8db-7f39-473b-9b21-343a40217507.png)

![Node List](https://user-images.githubusercontent.com/124823644/221171148-2a708022-0e5f-4e5e-86f6-311df9ca9e81.png)

![Node Map](https://user-images.githubusercontent.com/124823644/222127917-39acede4-04c4-4812-bf43-453a081e8218.png)

![Node Status](https://user-images.githubusercontent.com/124823644/221171183-62a35893-29e6-44a7-a690-67fb15ad748e.png)

![Settings(1)](https://user-images.githubusercontent.com/124823644/221171210-884bf3f5-0c98-4f80-897d-aa5b2cc0de64.png)

![Settings(2)](https://user-images.githubusercontent.com/124823644/222127962-73722d37-829f-4c01-8755-fe5d379031e5.png)

![Help](https://user-images.githubusercontent.com/124823644/221171250-94c9fdd7-4a93-43fd-a3d4-7787d53f29b0.png)

## Features

 - Real time data updated every 10 minutes
 - Summary of total active nodes
 - Breakdown of nodes by country, block height, agent version and network protocol
 - Filterable list of all nodes
 - Global map of filtered node locations which can be panned and zoomed
 - Ability to select detailed status of any node 
 - Monitoring of selected node status including pop up alerts and email alerts if any issues arise
 - Highly configurable behaviour including start with windows, minimize to tray and minimise on close
 
## Requirements

 - Network connection to the internet
 - No inbound ports need to be opened
 - Microsoft Windows operating system (I have tested on Windows 10 only)
 - Dot Net Framework 4.7.2. This will be installed at deployment time if not already present
 
## To Do List

- Fix issue with high background cpu usage
- Fix any other bugs identified by users
- Make enhancements identified by users
 
## Acknowledgements

Thanks to the following API providers used as the source of data for this application

- [Blockchair](https://blockchair.com/)
- [IP-API](https://ip-api.com/)
- [Github](https://github.com/)
- [OpenStreetMap](https://www.openstreetmap.org/)

Thanks to [Advanced Installer](https://www.advancedinstaller.com/) for their excellent installer which has been used from release 1.00 onwards
