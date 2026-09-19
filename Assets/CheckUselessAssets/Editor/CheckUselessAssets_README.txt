# 📦 USELESS ASSETS MANAGER 📦

Welcome to the **Useless Assets Manager**!

## What is this folder?
This folder contains assets that have been flagged as *useless* because they are not referenced by any enabled scene in your Build Settings. Think of it as a temporary holding area for assets that may be candidates for removal.
💡Specially useful when combined with GIT for cleaner commits! 

## 🔄 How to restore assets
You can easily restore these assets to their original locations:
1. **Right-click** on any asset in this folder and select:
   > **Check Useless Assets → Mark as Useful**
2. To restore multiple assets, select them all and use the same menu option.

## 💡 Tips for Managing Assets
- **Non-Destructive:** Assets here are NOT deleted—they’re simply moved out of your main project structure.
- **Automatic Tracking:** All original locations are recorded automatically.
- **Quick Selection:** Use **Check Useless Assets → Find and Select Useless Assets** to quickly view all assets that appear unused.
- **Statistics:** Run the statistics function to see what types of files are taking up space and how much.

## ⚠️ Important Warning
Before permanently deleting these assets, please verify that:
- They are not used in disabled scenes.
- They are not loaded dynamically by your scripts at runtime.
- They are not referenced by assets outside your Build Settings.

## 📝 Credits
Made with ❤️ using **CheckUselessAssets** by Uppercut Studio.
