# ⚡ Getting Started

Welcome to the Official Threadlink Documentation!

Below are the steps necessary to set up Threadlink in your Unity Project.

1. Make sure the following Unity packages are present in your project before installing Threadlink: \
   \- Input System\
   \- Addressables
2. Generate the necessary Addressable Assets for the project by navigating to the top menu and selecting Window -> Asset Management -> Addressables -> Groups -> Create Groups
3. Install Threadlink by importing the provided Unity package. Right-click anywhere in your project window and navigate to Import Custom Package, then find the Threadlink package in your local machine. Click Import once the Import Window pops up.
4. You have successfully installed Threadlink and are now ready to run the initial setup. On the top menu, click on the newly created Threadlink entry, and navigate to Initial Setup Wizard. In the wizard, you can select which scripting symbols Threadlink will use in your project.\
   \
   \- It is recommended that you enable Threadlink's Debugging/Logging System, Scribe, by including the `#THREADLINK_SCRIBE` symbol. This will provide access to a powerful Logging Interface that you can use during development.\
   \
   \- If you own the Odin Inspector plugin, remove the `#THREADLINK_INSPECTOR` symbol from the list, else include it, as it is necessary for some editor enhancements Threadlink provides.\
   \
   \- If you need to implement a 2.5D/3D Character Controller for your game, you can include either the `#THREADLINK_TEMPLATES_CONTROLLER_2D` or `#THREADLINK_TEMPLATES_CONTROLLER_3D` symbols.\
   \
   \- Threadlink integrates with various other assets and plugins. The FinalIK plugin can be used together with the Character Controller Template to provide additional IK features. Include the `#THREADLINK_INTEGRATIONS_FINALIK` symbol for FinalIK support of the template.
5. Done! You can now start developing your project using Threadlink!
