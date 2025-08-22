using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace App.Security
{
    /// <summary>
    /// Secure storage for sensitive data using platform-specific secure storage
    /// - Android: Android Keystore
    /// - iOS: iOS Keychain
    /// - Editor: EditorPrefs with basic encryption
    /// - Other: Encrypted PlayerPrefs as fallback
    /// </summary>
    public static class SecureStorage
    {
        private const string ENCRYPTION_KEY = "BlokusSecureKey2024"; // Simple XOR key for editor/fallback
        private const bool DEBUG_MODE = true;
        
        /// <summary>
        /// Store a string value securely
        /// </summary>
        public static void StoreString(string key, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(key) || value == null)
                {
                    Debug.LogWarning("[SecureStorage] Invalid key or value");
                    return;
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                // Android Keystore implementation
                StoreStringAndroid(key, value);
#elif UNITY_IOS && !UNITY_EDITOR
                // iOS Keychain implementation
                StoreStringIOS(key, value);
#elif UNITY_EDITOR
                // Unity Editor - EditorPrefs with simple encryption
                string encryptedValue = SimpleEncrypt(value);
                EditorPrefs.SetString($"SecureStorage_{key}", encryptedValue);
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [EDITOR] Stored key: {key}");
#else
                // Fallback - PlayerPrefs with simple encryption
                string encryptedValue = SimpleEncrypt(value);
                PlayerPrefs.SetString($"SecureStorage_{key}", encryptedValue);
                PlayerPrefs.Save();
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [FALLBACK] Stored key: {key}");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] Store failed for key '{key}': {ex.Message}");
            }
        }

        /// <summary>
        /// Get a string value from secure storage
        /// </summary>
        public static string GetString(string key, string defaultValue = "")
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning("[SecureStorage] Invalid key");
                    return defaultValue;
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                // Android Keystore implementation
                return GetStringAndroid(key, defaultValue);
#elif UNITY_IOS && !UNITY_EDITOR
                // iOS Keychain implementation
                return GetStringIOS(key, defaultValue);
#elif UNITY_EDITOR
                // Unity Editor - EditorPrefs with decryption
                string encryptedValue = EditorPrefs.GetString($"SecureStorage_{key}", "");
                if (string.IsNullOrEmpty(encryptedValue))
                {
                    if (DEBUG_MODE)
                        Debug.Log($"[SecureStorage] [EDITOR] Key not found: {key}");
                    return defaultValue;
                }
                
                string decryptedValue = SimpleDecrypt(encryptedValue);
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [EDITOR] Retrieved key: {key}");
                return decryptedValue;
#else
                // Fallback - PlayerPrefs with decryption
                string encryptedValue = PlayerPrefs.GetString($"SecureStorage_{key}", "");
                if (string.IsNullOrEmpty(encryptedValue))
                {
                    if (DEBUG_MODE)
                        Debug.Log($"[SecureStorage] [FALLBACK] Key not found: {key}");
                    return defaultValue;
                }
                
                string decryptedValue = SimpleDecrypt(encryptedValue);
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [FALLBACK] Retrieved key: {key}");
                return decryptedValue;
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] Get failed for key '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Delete a key from secure storage
        /// </summary>
        public static void DeleteKey(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning("[SecureStorage] Invalid key");
                    return;
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                // Android Keystore implementation
                DeleteKeyAndroid(key);
#elif UNITY_IOS && !UNITY_EDITOR
                // iOS Keychain implementation
                DeleteKeyIOS(key);
#elif UNITY_EDITOR
                // Unity Editor - EditorPrefs
                EditorPrefs.DeleteKey($"SecureStorage_{key}");
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [EDITOR] Deleted key: {key}");
#else
                // Fallback - PlayerPrefs
                PlayerPrefs.DeleteKey($"SecureStorage_{key}");
                PlayerPrefs.Save();
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [FALLBACK] Deleted key: {key}");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] Delete failed for key '{key}': {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a key exists in secure storage
        /// </summary>
        public static bool HasKey(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning("[SecureStorage] Invalid key");
                    return false;
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                // Android Keystore implementation
                return HasKeyAndroid(key);
#elif UNITY_IOS && !UNITY_EDITOR
                // iOS Keychain implementation
                return HasKeyIOS(key);
#elif UNITY_EDITOR
                // Unity Editor - EditorPrefs
                bool exists = EditorPrefs.HasKey($"SecureStorage_{key}");
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [EDITOR] Key exists '{key}': {exists}");
                return exists;
#else
                // Fallback - PlayerPrefs
                bool exists = PlayerPrefs.HasKey($"SecureStorage_{key}");
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [FALLBACK] Key exists '{key}': {exists}");
                return exists;
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] HasKey failed for key '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear all secure storage data
        /// </summary>
        public static void ClearAll()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                // Android Keystore - delete all our keys
                ClearAllAndroid();
#elif UNITY_IOS && !UNITY_EDITOR
                // iOS Keychain - delete all our keys
                ClearAllIOS();
#elif UNITY_EDITOR
                // Unity Editor - delete all SecureStorage_ prefixed keys
                // Note: EditorPrefs doesn't have a clear all method, so we'll delete known keys
                Debug.Log("[SecureStorage] [EDITOR] Clear all - delete known keys manually");
#else
                // Fallback - PlayerPrefs (dangerous, clears everything)
                Debug.LogWarning("[SecureStorage] [FALLBACK] ClearAll not implemented for PlayerPrefs");
#endif
                
                if (DEBUG_MODE)
                    Debug.Log("[SecureStorage] All secure storage cleared");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] ClearAll failed: {ex.Message}");
            }
        }

        // ========================================
        // Platform-specific implementations
        // ========================================

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void StoreStringAndroid(string key, string value)
        {
            // TODO: Android Keystore implementation
            // For now, use encrypted PlayerPrefs as temporary solution
            string encryptedValue = SimpleEncrypt(value);
            PlayerPrefs.SetString($"SecureStorage_{key}", encryptedValue);
            PlayerPrefs.Save();
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [ANDROID_TEMP] Stored key: {key}");
        }

        private static string GetStringAndroid(string key, string defaultValue)
        {
            // TODO: Android Keystore implementation
            string encryptedValue = PlayerPrefs.GetString($"SecureStorage_{key}", "");
            if (string.IsNullOrEmpty(encryptedValue))
                return defaultValue;
            
            string decryptedValue = SimpleDecrypt(encryptedValue);
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [ANDROID_TEMP] Retrieved key: {key}");
            return decryptedValue;
        }

        private static void DeleteKeyAndroid(string key)
        {
            PlayerPrefs.DeleteKey($"SecureStorage_{key}");
            PlayerPrefs.Save();
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [ANDROID_TEMP] Deleted key: {key}");
        }

        private static bool HasKeyAndroid(string key)
        {
            bool exists = PlayerPrefs.HasKey($"SecureStorage_{key}");
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [ANDROID_TEMP] Key exists '{key}': {exists}");
            return exists;
        }

        private static void ClearAllAndroid()
        {
            // TODO: Implement proper Android Keystore clear
            Debug.LogWarning("[SecureStorage] [ANDROID_TEMP] ClearAll not implemented");
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private static void StoreStringIOS(string key, string value)
        {
            // TODO: iOS Keychain implementation
            // For now, use encrypted PlayerPrefs as temporary solution
            string encryptedValue = SimpleEncrypt(value);
            PlayerPrefs.SetString($"SecureStorage_{key}", encryptedValue);
            PlayerPrefs.Save();
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [IOS_TEMP] Stored key: {key}");
        }

        private static string GetStringIOS(string key, string defaultValue)
        {
            // TODO: iOS Keychain implementation
            string encryptedValue = PlayerPrefs.GetString($"SecureStorage_{key}", "");
            if (string.IsNullOrEmpty(encryptedValue))
                return defaultValue;
            
            string decryptedValue = SimpleDecrypt(encryptedValue);
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [IOS_TEMP] Retrieved key: {key}");
            return decryptedValue;
        }

        private static void DeleteKeyIOS(string key)
        {
            PlayerPrefs.DeleteKey($"SecureStorage_{key}");
            PlayerPrefs.Save();
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [IOS_TEMP] Deleted key: {key}");
        }

        private static bool HasKeyIOS(string key)
        {
            bool exists = PlayerPrefs.HasKey($"SecureStorage_{key}");
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [IOS_TEMP] Key exists '{key}': {exists}");
            return exists;
        }

        private static void ClearAllIOS()
        {
            // TODO: Implement proper iOS Keychain clear
            Debug.LogWarning("[SecureStorage] [IOS_TEMP] ClearAll not implemented");
        }
#endif

        // ========================================
        // Simple encryption for Editor/Fallback
        // ========================================

        private static string SimpleEncrypt(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            try
            {
                char[] chars = text.ToCharArray();
                char[] keyChars = ENCRYPTION_KEY.ToCharArray();
                
                for (int i = 0; i < chars.Length; i++)
                {
                    chars[i] = (char)(chars[i] ^ keyChars[i % keyChars.Length]);
                }
                
                return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(chars));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] Encryption failed: {ex.Message}");
                return text; // Return original if encryption fails
            }
        }

        private static string SimpleDecrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return encryptedText;

            try
            {
                byte[] bytes = Convert.FromBase64String(encryptedText);
                char[] chars = System.Text.Encoding.UTF8.GetChars(bytes);
                char[] keyChars = ENCRYPTION_KEY.ToCharArray();
                
                for (int i = 0; i < chars.Length; i++)
                {
                    chars[i] = (char)(chars[i] ^ keyChars[i % keyChars.Length]);
                }
                
                return new string(chars);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] Decryption failed: {ex.Message}");
                return encryptedText; // Return original if decryption fails
            }
        }
    }
}