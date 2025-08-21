Instruction to Install and Run Assist Software:

1.	Unzip Assist_Setup folder.
2.	Install the setup using default location.
3.	Open cmd as an administrator


4.	Write the following command : cd “C:\Windows\Microsoft.NET\Framework\v4.0.30319\” and press Enter.


5.	Now write the following command : installutil.exe “<path of Assist_Service.exe>” and press Enter.
 

6.	Create Inbound rule for Port: “12345”, “12346”, “12347” by using the follow command on windows powershell: New-NetFirewallRule -DisplayName "Allow Port 12345 UDP" -Direction Inbound -LocalPort 12345 -Protocol UDP -Action Allow
7.	Now connect the computers in the peer-to-peer network. I use to turn on mobile hotspot on PC-1 and connect the laptop with it to form a network.
8.	Then start the windows service using following command: net start Service1.
 
9.	Meanwhile run the Assist_TSR as an administrator mode.
                                     
10.	You will see an Assist icon in the system tray.
11.	Double click on that icon and start the Assist_TSR.
 


12.	Rename the nodes name accordingly. To test opening VLC in Node1 and triggering open VS Code on Node2. Rename Your first pc as “Node1” and second pc as “Node2”.
13.	On the configurations tab, you can see both connected nodes.
14.	Now launch the application on Node1 you will observe the launch of another application on Node2.
15.	You can Reconfigure these rules according to your choice.
16.	Closing of one application will close all associated application on neighboring nodes.
