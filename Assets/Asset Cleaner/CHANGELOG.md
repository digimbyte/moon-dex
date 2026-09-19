# Asset Cleaner Changelog

If you find this asset useful, please leave an honest review on the [Asset Store](https://assetstore.unity.com/packages/tools/utilities/asset-cleaner-pro-clean-find-references-167990#reviews)!

## Version 1.50 (current)

Released: Dec 31, 2025

### Improvements & fixes in v. 1.50

- **Fixed editor freeze** caused by infinite loop in SerializedProperty traversal
- **Non-blocking initialization**: cache now builds in background without freezing editor, even in large projects
- **Persistent disk cache**: cache is saved to disk and restored on next editor launch (invalidated automatically when assets change)
- **Significantly faster cache building**
- **Full compatibility with Unity 6.3**

---

## Version 1.33

Released: Nov 13, 2024

### Improvements & fixes in v. 1.33

- Fixed error when broken asset (built-in in Unity 6, most probably in ProjectSettings) halted the building cache and made window unusable
- Edge case scenario warnings fix for MacOS
- Fix for Unity warnings on import
- Support for usage tracking inside packages added (thanks to Matt)
- Slightly increased cache building speed
- Small fixes
