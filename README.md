# subnets-to-route-commands

This program converts file with list of subnets (like 172.17.9.0/24, one per line) to windows batch file with 'route add' commands.  
  
Required parameters:  
-i <file with subnets> - Input file  
-if - Interface id (as listed by 'route print' command)  
-gw - Gateway address  
-m - Metric  

Output parameters (at least one is required):  
-of <output file name> - Output commands to the specified file  
-oc - Output commands to console  

Optional parameters:  
-p - Add 'pause' after each 'route' command  