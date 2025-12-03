using System;
using UnityEngine;
using System.Runtime.InteropServices;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace App.Security
{
    /// <summary>
    /// 플랫폼별 보안 저장소를 사용하여 민감한 데이터를 안전하게 저장
    /// - Android: Android Keystore
    /// - iOS: iOS Keychain
    /// - Editor: EditorPrefs (기본 암호화)
    /// - Other: 암호화된 PlayerPrefs (폴백)
    /// </summary>
    public static class SecureStorage
    {
        private const string ENCRYPTION_KEY = "BlobloSecureKey2025"; // Editor/Fallback용 XOR 암호화 키
        private const bool DEBUG_MODE = true;

        // ========================================
        // 오류 처리 및 가용성 확인
        // ========================================

        /// <summary>
        /// 보안 저장소가 사용 가능하고 정상 작동하는지 확인
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return IsAndroidKeystoreAvailable();
#elif UNITY_IOS && !UNITY_EDITOR
                return IsIOSKeychainAvailable();
#elif UNITY_EDITOR
                return true; // Editor always available
#else
                return true; // Fallback always available
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] Availability check failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 현재 플랫폼의 보안 저장소 정보 반환
        /// </summary>
        public static string GetPlatformInfo()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            bool keystoreAvailable = IsAndroidKeystoreAvailable();
            return $"Android Keystore: {(keystoreAvailable ? "Available" : "Unavailable - Using encrypted fallback")}";
#elif UNITY_IOS && !UNITY_EDITOR
            bool keychainAvailable = IsIOSKeychainAvailable();
            return $"iOS Keychain: {(keychainAvailable ? "Available" : "Unavailable - Using encrypted fallback")}";
#elif UNITY_EDITOR
            return "Unity Editor: EditorPrefs with XOR encryption";
#else
            return "Fallback: PlayerPrefs with XOR encryption";
#endif
        }
        
        /// <summary>
        /// 문자열 값을 안전하게 저장
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
        /// 보안 저장소에서 문자열 값 가져오기
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
        /// 보안 저장소에서 키 삭제
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
        /// 보안 저장소에 키가 존재하는지 확인
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
        /// 모든 보안 저장소 데이터 삭제
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
        // 이중 저장 메커니즘 (Primary + Backup)
        // ========================================

        /// <summary>
        /// RefreshToken과 같은 중요 데이터를 백업 메커니즘과 함께 저장
        /// Primary 보안 저장소(Keystore/Keychain)와 암호화된 PlayerPrefs 백업에 모두 저장
        /// </summary>
        public static void StoreStringWithBackup(string key, string value)
        {
            try
            {
                if (string.IsNullOrEmpty(key) || value == null)
                {
                    Debug.LogWarning("[SecureStorage] 백업 저장을 위한 키 또는 값이 유효하지 않음");
                    return;
                }

                // 1) Primary 보안 저장소에 저장 (Keystore/Keychain)
                StoreString(key, value);

                // 2) Backup 저장소에 저장 (암호화된 PlayerPrefs - 항상 성공)
                try
                {
                    string encrypted = SimpleEncrypt(value);
                    PlayerPrefs.SetString($"Backup_{key}", encrypted);
                    PlayerPrefs.Save();

                    if (DEBUG_MODE)
                        Debug.Log($"[SecureStorage] 백업과 함께 저장 완료: {key}");
                }
                catch (Exception backupEx)
                {
                    Debug.LogError($"[SecureStorage] 백업 저장 실패 '{key}': {backupEx.Message}");
                    // Primary 저장은 성공했으므로 예외를 던지지 않음
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] 백업 저장 실패 '{key}': {ex.Message}");

                // 최후의 수단으로 백업 전용 저장 시도
                try
                {
                    string encrypted = SimpleEncrypt(value);
                    PlayerPrefs.SetString($"Backup_{key}", encrypted);
                    PlayerPrefs.Save();
                    Debug.LogWarning($"[SecureStorage] Primary 저장 실패, 백업만 저장됨: {key}");
                }
                catch (Exception backupEx)
                {
                    Debug.LogError($"[SecureStorage] 완전한 저장 실패 '{key}': {backupEx.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 자동 백업 복구 기능이 있는 문자열 값 가져오기
        /// Primary 저장소를 먼저 시도하고, 실패 시 백업에서 복구
        /// </summary>
        public static string GetStringWithBackup(string key, string defaultValue = "")
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning("[SecureStorage] 백업 조회를 위한 키가 유효하지 않음");
                    return defaultValue;
                }

                // 1) Primary 보안 저장소 먼저 시도
                string value = GetString(key, "");
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }

                // 2) Primary 저장소가 비어있거나 실패 - 백업 시도
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] Primary 저장소가 비어있음, 백업 시도: {key}");

                string backup = PlayerPrefs.GetString($"Backup_{key}", "");
                if (string.IsNullOrEmpty(backup))
                {
                    if (DEBUG_MODE)
                        Debug.Log($"[SecureStorage] 백업을 찾을 수 없음: {key}");
                    return defaultValue;
                }

                // 3) 백업 데이터 복호화
                string decrypted = SimpleDecrypt(backup);
                if (string.IsNullOrEmpty(decrypted))
                {
                    Debug.LogWarning($"[SecureStorage] 백업 복호화 실패: {key}");
                    return defaultValue;
                }

                // 4) 복구 성공! Primary 저장소에 복원
                try
                {
                    StoreString(key, decrypted);
                    Debug.Log($"[SecureStorage] 백업에서 복구 성공 및 Primary 저장소 복원: {key}");
                }
                catch (Exception restoreEx)
                {
                    Debug.LogWarning($"[SecureStorage] 백업에서 복구했으나 Primary 복원 실패: {restoreEx.Message}");
                    // 복구된 값은 반환
                }

                return decrypted;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] 백업 조회 실패 '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Primary 및 Backup 저장소 모두에서 키 삭제
        /// </summary>
        public static void DeleteKeyWithBackup(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning("[SecureStorage] 백업 삭제를 위한 키가 유효하지 않음");
                    return;
                }

                // Primary 저장소에서 삭제
                DeleteKey(key);

                // Backup 저장소에서 삭제
                PlayerPrefs.DeleteKey($"Backup_{key}");
                PlayerPrefs.Save();

                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] 백업과 함께 삭제 완료: {key}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] 백업 삭제 실패 '{key}': {ex.Message}");
            }
        }

        // ========================================
        // 플랫폼별 구현
        // ========================================

#if UNITY_ANDROID && !UNITY_EDITOR
        private const string KEYSTORE_ALIAS = "BlokusSecureStorageKey";
        private const string ANDROID_KEYSTORE = "AndroidKeyStore";
        private const string TRANSFORMATION = "AES/GCM/NoPadding";
        
        private static void StoreStringAndroid(string key, string value)
        {
            try
            {
                // Android Keystore를 사용한 AES 암호화
                if (EnsureKeyExists())
                {
                    string encryptedValue = EncryptWithKeystore(value);
                    if (!string.IsNullOrEmpty(encryptedValue))
                    {
                        // SharedPreferences에 암호화된 데이터 저장
                        SetSharedPreference($"SecureStorage_{key}", encryptedValue);
                        if (DEBUG_MODE)
                            Debug.Log($"[SecureStorage] [ANDROID_KEYSTORE] Stored key: {key}");
                        return;
                    }
                }
                
                // Keystore 실패 시 폴백
                Debug.LogWarning($"[SecureStorage] [ANDROID] Keystore failed, using encrypted fallback for key: {key}");
                StoreStringAndroidFallback(key, value);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] Store failed: {ex.Message}");
                // Attempt to repair Keystore if it's corrupted
                if (ex.Message.Contains("KeyStore") || ex.Message.Contains("InvalidKey"))
                {
                    Debug.LogWarning("[SecureStorage] [ANDROID] Attempting Keystore repair");
                    try
                    {
                        DeleteKeystoreKey();
                    }
                    catch (Exception repairEx)
                    {
                        Debug.LogError($"[SecureStorage] [ANDROID] Keystore repair failed: {repairEx.Message}");
                    }
                }
                StoreStringAndroidFallback(key, value);
            }
        }

        private static string GetStringAndroid(string key, string defaultValue)
        {
            try
            {
                // 1) SharedPreferences 우선
                string encryptedValue = GetSharedPreference($"SecureStorage_{key}", "");

                if (string.IsNullOrEmpty(encryptedValue))
                {
                    // 2) SharedPreferences가 비어있음 - 백업에서 복구 시도
                    string recoveredValue = TryRecoverFromBackup(key);
                    if (!string.IsNullOrEmpty(recoveredValue))
                    {
                        Debug.Log($"[SecureStorage] [ANDROID] 백업에서 복구 성공: {key}");
                        // 복구된 데이터를 Primary 저장소에 재저장
                        try
                        {
                            StoreStringAndroid(key, recoveredValue);
                        }
                        catch (Exception storeEx)
                        {
                            Debug.LogWarning($"[SecureStorage] [ANDROID] 복구 후 재저장 실패: {storeEx.Message}");
                        }
                        return recoveredValue;
                    }

                    // 3) 백업도 없으면 폴백 확인
                    var fb = GetStringAndroidFallback(key, defaultValue);
                    if (!string.IsNullOrEmpty(fb) && fb != defaultValue)
                    {
                        if (DEBUG_MODE) Debug.Log($"[SecureStorage][ANDROID_FALLBACK] Retrieved key: {key}");
                        return fb;
                    }
                    return defaultValue;
                }

                // 4) Keystore로 복호화 시도
                if (EnsureKeyExists())
                {
                    string decryptedValue = DecryptWithKeystore(encryptedValue);
                    if (!string.IsNullOrEmpty(decryptedValue))
                    {
                        if (DEBUG_MODE)
                            Debug.Log($"[SecureStorage] [ANDROID_KEYSTORE] Retrieved key: {key}");
                        return decryptedValue;
                    }
                }

                // 5) Keystore 복호화 실패 - 백업에서 복구 시도
                Debug.LogWarning($"[SecureStorage] [ANDROID] Keystore 복호화 실패, 백업에서 복구 시도: {key}");
                string recoveredFromBackup = TryRecoverFromBackup(key);
                if (!string.IsNullOrEmpty(recoveredFromBackup))
                {
                    Debug.Log($"[SecureStorage] [ANDROID] 백업에서 복구 성공, Keystore 재생성 시도: {key}");

                    // Keystore 재생성 시도
                    if (TryRegenerateKeystoreAndRestore(key, recoveredFromBackup))
                    {
                        Debug.Log($"[SecureStorage] [ANDROID] Keystore 재생성 및 데이터 복원 성공: {key}");
                        return recoveredFromBackup;
                    }

                    // Keystore 재생성 실패해도 복구된 값은 반환
                    return recoveredFromBackup;
                }

                // 6) 백업도 실패 - 폴백 시도
                Debug.LogWarning($"[SecureStorage] [ANDROID] 백업 복구 실패, 폴백 시도: {key}");
                return GetStringAndroidFallback(key, defaultValue);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] Get failed: {ex.Message}");

                // Keystore 손상 감지
                if (ex.Message.Contains("KeyStore") || ex.Message.Contains("InvalidKey") ||
                    ex.Message.Contains("decrypt") || ex.Message.Contains("BadPadding"))
                {
                    Debug.LogWarning($"[SecureStorage] [ANDROID] Keystore 손상 감지: {key}");

                    // 백업에서 복구 시도
                    string recoveredValue = TryRecoverFromBackup(key);
                    if (!string.IsNullOrEmpty(recoveredValue))
                    {
                        Debug.Log($"[SecureStorage] [ANDROID] 예외 발생 후 백업에서 복구 성공: {key}");

                        // 손상된 데이터 정리
                        try
                        {
                            RemoveSharedPreference($"SecureStorage_{key}");
                        }
                        catch { /* 정리 실패는 무시 */ }

                        // Keystore 재생성 및 복원 시도
                        TryRegenerateKeystoreAndRestore(key, recoveredValue);

                        return recoveredValue;
                    }
                }

                // 최후의 폴백
                return GetStringAndroidFallback(key, defaultValue);
            }
        }

        /// <summary>
        /// Backup_키에서 데이터 복구 시도
        /// </summary>
        private static string TryRecoverFromBackup(string key)
        {
            try
            {
                string backup = PlayerPrefs.GetString($"Backup_{key}", "");
                if (string.IsNullOrEmpty(backup))
                    return null;

                string decrypted = SimpleDecrypt(backup);
                if (!string.IsNullOrEmpty(decrypted))
                {
                    if (DEBUG_MODE)
                        Debug.Log($"[SecureStorage] [ANDROID] 백업 복호화 성공: {key}");
                    return decrypted;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] 백업 복구 실패: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Keystore 재생성 및 데이터 복원 시도
        /// </summary>
        private static bool TryRegenerateKeystoreAndRestore(string key, string value)
        {
            try
            {
                Debug.Log($"[SecureStorage] [ANDROID] Keystore 재생성 시작: {key}");

                // 기존 Keystore 키 삭제
                try
                {
                    DeleteKeystoreKey();
                    Debug.Log($"[SecureStorage] [ANDROID] 기존 Keystore 키 삭제 완료");
                }
                catch (Exception deleteEx)
                {
                    Debug.LogWarning($"[SecureStorage] [ANDROID] Keystore 키 삭제 실패 (무시): {deleteEx.Message}");
                }

                // 새 Keystore 키 생성
                if (!GenerateKeystoreKey())
                {
                    Debug.LogError($"[SecureStorage] [ANDROID] Keystore 키 재생성 실패");
                    return false;
                }

                Debug.Log($"[SecureStorage] [ANDROID] 새 Keystore 키 생성 완료");

                // 복구된 데이터를 새 Keystore로 재암호화하여 저장
                StoreStringAndroid(key, value);
                Debug.Log($"[SecureStorage] [ANDROID] 데이터 재암호화 및 저장 완료: {key}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] Keystore 재생성 실패: {ex.Message}");
                return false;
            }
        }

        private static void DeleteKeyAndroid(string key)
        {
            try
            {
                RemoveSharedPreference($"SecureStorage_{key}");
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [ANDROID_KEYSTORE] Deleted key: {key}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] Delete failed: {ex.Message}");
                // 폴백으로 PlayerPrefs 삭제도 시도
                PlayerPrefs.DeleteKey($"SecureStorage_{key}");
                PlayerPrefs.Save();
            }
        }

        private static bool HasKeyAndroid(string key)
        {
            try
            {
                bool exists = HasSharedPreference($"SecureStorage_{key}");
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [ANDROID_KEYSTORE] Key exists '{key}': {exists}");
                return exists;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] HasKey failed: {ex.Message}");
                return PlayerPrefs.HasKey($"SecureStorage_{key}");
            }
        }

        private static void ClearAllAndroid()
        {
            try
            {
                // Keystore 키 삭제
                DeleteKeystoreKey();
                
                // SharedPreferences에서 SecureStorage 관련 키들 삭제
                ClearSecureStoragePreferences();
                
                if (DEBUG_MODE)
                    Debug.Log("[SecureStorage] [ANDROID_KEYSTORE] All secure storage cleared");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] ClearAll failed: {ex.Message}");
            }
        }

        // ========================================
        // Android Keystore Helper Methods
        // ========================================

        private static bool EnsureKeyExists()
        {
            try
            {
                using (AndroidJavaClass keyStoreClass = new AndroidJavaClass("java.security.KeyStore"))
                {
                    AndroidJavaObject keyStore = keyStoreClass.CallStatic<AndroidJavaObject>("getInstance", ANDROID_KEYSTORE);
                    keyStore.Call("load", (AndroidJavaObject)null);
                    
                    // 키가 이미 존재하는지 확인
                    bool keyExists = keyStore.Call<bool>("containsAlias", KEYSTORE_ALIAS);
                    if (keyExists)
                        return true;
                    
                    // 키가 없으면 생성
                    return GenerateKeystoreKey();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] EnsureKeyExists failed: {ex.Message}");
                return false;
            }
        }

        private static bool GenerateKeystoreKey()
        {
            try
            {
                using (AndroidJavaClass keyGenClass = new AndroidJavaClass("javax.crypto.KeyGenerator"))
                using (AndroidJavaClass keyGenSpecBuilderClass = new AndroidJavaClass("android.security.keystore.KeyGenParameterSpec$Builder"))
                using (AndroidJavaClass keyPropertiesClass = new AndroidJavaClass("android.security.keystore.KeyProperties"))
                {
                    AndroidJavaObject keyGenerator = keyGenClass.CallStatic<AndroidJavaObject>("getInstance", "AES", ANDROID_KEYSTORE);
                    
                    // KeyGenParameterSpec 생성
                    int purposes = keyPropertiesClass.GetStatic<int>("PURPOSE_ENCRYPT") | keyPropertiesClass.GetStatic<int>("PURPOSE_DECRYPT");
                    AndroidJavaObject builder = new AndroidJavaObject("android.security.keystore.KeyGenParameterSpec$Builder", KEYSTORE_ALIAS, purposes);
                    
                    builder.Call<AndroidJavaObject>("setBlockModes", keyPropertiesClass.GetStatic<string>("BLOCK_MODE_GCM"));
                    builder.Call<AndroidJavaObject>("setEncryptionPaddings", keyPropertiesClass.GetStatic<string>("ENCRYPTION_PADDING_NONE"));
                    builder.Call<AndroidJavaObject>("setKeySize", 256);
                    
                    AndroidJavaObject keyGenSpec = builder.Call<AndroidJavaObject>("build");
                    keyGenerator.Call("init", keyGenSpec);
                    keyGenerator.Call<AndroidJavaObject>("generateKey");
                    
                    if (DEBUG_MODE)
                        Debug.Log("[SecureStorage] [ANDROID] Keystore key generated successfully");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] GenerateKeystoreKey failed: {ex.Message}");
                return false;
            }
        }

        private static string EncryptWithKeystore(string plaintext)
        {
            try
            {
                using (AndroidJavaClass keyStoreClass = new AndroidJavaClass("java.security.KeyStore"))
                using (AndroidJavaClass cipherClass = new AndroidJavaClass("javax.crypto.Cipher"))
                using (AndroidJavaClass base64Class = new AndroidJavaClass("android.util.Base64"))
                {
                    AndroidJavaObject keyStore = keyStoreClass.CallStatic<AndroidJavaObject>("getInstance", ANDROID_KEYSTORE);
                    keyStore.Call("load", (AndroidJavaObject)null);
                    
                    AndroidJavaObject secretKey = keyStore.Call<AndroidJavaObject>("getKey", KEYSTORE_ALIAS, (AndroidJavaObject)null);
                    AndroidJavaObject cipher = cipherClass.CallStatic<AndroidJavaObject>("getInstance", TRANSFORMATION);
                    
                    cipher.Call("init", 1, secretKey); // ENCRYPT_MODE = 1
                    
                    byte[] plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
                    
                    // byte[] → AndroidJavaObject 변환을 위해 직접 doFinal에 전달
                    AndroidJavaObject encryptedBytes = cipher.Call<AndroidJavaObject>("doFinal", plaintextBytes);
                    AndroidJavaObject iv = cipher.Call<AndroidJavaObject>("getIV");
                    
                    // IV + 암호화된 데이터를 Base64로 인코딩
                    string ivBase64 = base64Class.CallStatic<string>("encodeToString", iv, 0);
                    string encryptedBase64 = base64Class.CallStatic<string>("encodeToString", encryptedBytes, 0);
                    
                    return ivBase64 + ":" + encryptedBase64;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] EncryptWithKeystore failed: {ex.Message}");
                return null;
            }
        }

        private static string DecryptWithKeystore(string encryptedData)
        {
            try
            {
                string[] parts = encryptedData.Split(':');
                if (parts.Length != 2)
                    return null;
                
                using (AndroidJavaClass keyStoreClass = new AndroidJavaClass("java.security.KeyStore"))
                using (AndroidJavaClass cipherClass = new AndroidJavaClass("javax.crypto.Cipher"))
                using (AndroidJavaClass base64Class = new AndroidJavaClass("android.util.Base64"))
                using (AndroidJavaClass gcmSpecClass = new AndroidJavaClass("javax.crypto.spec.GCMParameterSpec"))
                {
                    AndroidJavaObject keyStore = keyStoreClass.CallStatic<AndroidJavaObject>("getInstance", ANDROID_KEYSTORE);
                    keyStore.Call("load", (AndroidJavaObject)null);
                    
                    AndroidJavaObject secretKey = keyStore.Call<AndroidJavaObject>("getKey", KEYSTORE_ALIAS, (AndroidJavaObject)null);
                    AndroidJavaObject cipher = cipherClass.CallStatic<AndroidJavaObject>("getInstance", TRANSFORMATION);
                    
                    AndroidJavaObject iv = base64Class.CallStatic<AndroidJavaObject>("decode", parts[0], 0);
                    AndroidJavaObject gcmSpec = new AndroidJavaObject("javax.crypto.spec.GCMParameterSpec", 128, iv);
                    
                    cipher.Call("init", 2, secretKey, gcmSpec); // DECRYPT_MODE = 2
                    
                    AndroidJavaObject encryptedBytes = base64Class.CallStatic<AndroidJavaObject>("decode", parts[1], 0);
                    AndroidJavaObject decryptedBytes = cipher.Call<AndroidJavaObject>("doFinal", encryptedBytes);
                    
                    // byte[]를 string으로 변환
                    byte[] managedBytes = AndroidJNIHelper.ConvertFromJNIArray<byte[]>(decryptedBytes.GetRawObject());
                    return System.Text.Encoding.UTF8.GetString(managedBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] DecryptWithKeystore failed: {ex.Message}");
                return null;
            }
        }

        private static void DeleteKeystoreKey()
        {
            try
            {
                using (AndroidJavaClass keyStoreClass = new AndroidJavaClass("java.security.KeyStore"))
                {
                    AndroidJavaObject keyStore = keyStoreClass.CallStatic<AndroidJavaObject>("getInstance", ANDROID_KEYSTORE);
                    keyStore.Call("load", (AndroidJavaObject)null);
                    keyStore.Call("deleteEntry", KEYSTORE_ALIAS);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [ANDROID] DeleteKeystoreKey failed: {ex.Message}");
            }
        }

        // ========================================
        // SharedPreferences Helper Methods
        // ========================================

        private static void SetSharedPreference(string key, string value)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                AndroidJavaObject sharedPref = context.Call<AndroidJavaObject>("getSharedPreferences", "BlokusSecureStorage", 0);
                AndroidJavaObject editor = sharedPref.Call<AndroidJavaObject>("edit");
                editor.Call<AndroidJavaObject>("putString", key, value);
                editor.Call<bool>("commit");
            }
        }

        private static string GetSharedPreference(string key, string defaultValue)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                AndroidJavaObject sharedPref = context.Call<AndroidJavaObject>("getSharedPreferences", "BlokusSecureStorage", 0);
                return sharedPref.Call<string>("getString", key, defaultValue);
            }
        }

        private static void RemoveSharedPreference(string key)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                AndroidJavaObject sharedPref = context.Call<AndroidJavaObject>("getSharedPreferences", "BlokusSecureStorage", 0);
                AndroidJavaObject editor = sharedPref.Call<AndroidJavaObject>("edit");
                editor.Call<AndroidJavaObject>("remove", key);
                editor.Call<bool>("commit");
            }
        }

        private static bool HasSharedPreference(string key)
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                AndroidJavaObject sharedPref = context.Call<AndroidJavaObject>("getSharedPreferences", "BlokusSecureStorage", 0);
                return sharedPref.Call<bool>("contains", key);
            }
        }

        private static void ClearSecureStoragePreferences()
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                AndroidJavaObject sharedPref = context.Call<AndroidJavaObject>("getSharedPreferences", "BlokusSecureStorage", 0);
                AndroidJavaObject editor = sharedPref.Call<AndroidJavaObject>("edit");
                editor.Call<AndroidJavaObject>("clear");
                editor.Call<bool>("commit");
            }
        }

        // ========================================
        // Android Fallback Methods (암호화된 PlayerPrefs)
        // ========================================

        private static void StoreStringAndroidFallback(string key, string value)
        {
            string encryptedValue = SimpleEncrypt(value);
            PlayerPrefs.SetString($"SecureStorage_{key}", encryptedValue);
            PlayerPrefs.Save();
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [ANDROID_FALLBACK] Stored key: {key}");
        }

        private static string GetStringAndroidFallback(string key, string defaultValue)
        {
            string encryptedValue = PlayerPrefs.GetString($"SecureStorage_{key}", "");
            if (string.IsNullOrEmpty(encryptedValue))
                return defaultValue;
            
            string decryptedValue = SimpleDecrypt(encryptedValue);
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [ANDROID_FALLBACK] Retrieved key: {key}");
            return decryptedValue;
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        // iOS Keychain Native Plugin P/Invoke Declarations
        [DllImport("__Internal")]
        private static extern int _SecureStorage_StoreString(string key, string value);
        
        [DllImport("__Internal")]
        private static extern System.IntPtr _SecureStorage_GetString(string key, string defaultValue);
        
        [DllImport("__Internal")]
        private static extern int _SecureStorage_DeleteKey(string key);
        
        [DllImport("__Internal")]
        private static extern int _SecureStorage_HasKey(string key);
        
        [DllImport("__Internal")]
        private static extern int _SecureStorage_ClearAll();
        
        [DllImport("__Internal")]
        private static extern int _SecureStorage_IsAvailable();

        private static void StoreStringIOS(string key, string value)
        {
            try
            {
                // Check Keychain availability first
                if (_SecureStorage_IsAvailable() == 0)
                {
                    Debug.LogWarning("[SecureStorage] [IOS] Keychain unavailable, using encrypted fallback");
                    StoreStringIOSFallback(key, value);
                    return;
                }
                
                int result = _SecureStorage_StoreString(key, value);
                if (result == 1)
                {
                    if (DEBUG_MODE)
                        Debug.Log($"[SecureStorage] [IOS_KEYCHAIN] Stored key: {key}");
                }
                else
                {
                    Debug.LogWarning($"[SecureStorage] [IOS] Keychain store failed, using encrypted fallback for key: {key}");
                    StoreStringIOSFallback(key, value);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [IOS] Store failed: {ex.Message}");
                // Check for common Keychain errors
                if (ex.Message.Contains("-34018") || ex.Message.Contains("errSecMissingEntitlement"))
                {
                    Debug.LogError("[SecureStorage] [IOS] Keychain entitlement missing - check iOS project settings");
                }
                else if (ex.Message.Contains("-25300") || ex.Message.Contains("errSecItemNotFound"))
                {
                    Debug.LogWarning("[SecureStorage] [IOS] Keychain item not found during store operation");
                }
                StoreStringIOSFallback(key, value);
            }
        }

        private static string GetStringIOS(string key, string defaultValue)
        {
            try
            {
                // Step 1: Keychain 사용 가능 여부 체크
                if (_SecureStorage_IsAvailable() == 0)
                {
                    Debug.LogWarning("[SecureStorage] [IOS] Keychain 사용 불가, 백업에서 복구 시도");

                    // Step 2: 백업에서 복구 시도
                    string recoveredValue = TryRecoverFromBackup(key);
                    if (!string.IsNullOrEmpty(recoveredValue))
                    {
                        Debug.Log($"[SecureStorage] [IOS] 백업에서 복구 성공: {key}");
                        return recoveredValue;
                    }

                    // Step 3: 폴백으로 시도 (암호화된 PlayerPrefs)
                    return GetStringIOSFallback(key, defaultValue);
                }

                // Step 4: Keychain에서 값 조회 시도
                System.IntPtr ptr = _SecureStorage_GetString(key, defaultValue);
                if (ptr != System.IntPtr.Zero)
                {
                    string result = Marshal.PtrToStringAnsi(ptr);
                    Marshal.FreeHGlobal(ptr); // Free the allocated string from native code

                    if (!string.IsNullOrEmpty(result) && result != defaultValue)
                    {
                        if (DEBUG_MODE)
                            Debug.Log($"[SecureStorage] [IOS_KEYCHAIN] 조회 성공: {key}");
                        return result;
                    }
                }

                // Step 5: Keychain이 비어있거나 실패 - 백업에서 복구 시도
                string backupValue = TryRecoverFromBackup(key);
                if (!string.IsNullOrEmpty(backupValue))
                {
                    Debug.Log($"[SecureStorage] [IOS] Keychain 실패 후 백업에서 복구: {key}");

                    // Keychain에 복원된 데이터 재저장 시도
                    try
                    {
                        int storeResult = _SecureStorage_StoreString(key, backupValue);
                        if (storeResult == 1)
                        {
                            Debug.Log($"[SecureStorage] [IOS] Keychain에 데이터 복원 완료: {key}");
                        }
                        else
                        {
                            Debug.LogWarning($"[SecureStorage] [IOS] Keychain 복원 실패하였으나 백업 데이터는 사용 가능: {key}");
                        }
                    }
                    catch (Exception restoreEx)
                    {
                        Debug.LogWarning($"[SecureStorage] [IOS] Keychain 복원 중 예외 발생 (백업 데이터는 사용 가능): {restoreEx.Message}");
                    }

                    return backupValue;
                }

                // Step 6: 최종 폴백 - 암호화된 PlayerPrefs
                return GetStringIOSFallback(key, defaultValue);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [IOS] 조회 실패: {ex.Message}");

                // iOS Keychain 일반적인 에러 처리
                bool isKeychainError = false;

                if (ex.Message.Contains("-25300") || ex.Message.Contains("errSecItemNotFound"))
                {
                    Debug.LogWarning($"[SecureStorage] [IOS] Keychain 항목 없음: {key}");
                    isKeychainError = true;
                }
                else if (ex.Message.Contains("-34018") || ex.Message.Contains("errSecIO"))
                {
                    Debug.LogError("[SecureStorage] [IOS] Keychain I/O 에러 (접근 권한 또는 Entitlement 문제)");
                    isKeychainError = true;
                }
                else if (ex.Message.Contains("-25293") || ex.Message.Contains("errSecAuthFailed"))
                {
                    Debug.LogError("[SecureStorage] [IOS] Keychain 인증 실패");
                    isKeychainError = true;
                }
                else if (ex.Message.Contains("errSecMissingEntitlement"))
                {
                    Debug.LogError("[SecureStorage] [IOS] Keychain Entitlement 누락 - iOS 프로젝트 설정 확인 필요");
                    isKeychainError = true;
                }

                // Keychain 에러인 경우 백업 복구 시도
                if (isKeychainError)
                {
                    string recoveredValue = TryRecoverFromBackup(key);
                    if (!string.IsNullOrEmpty(recoveredValue))
                    {
                        Debug.Log($"[SecureStorage] [IOS] 예외 발생 후 백업에서 복구: {key}");

                        // Keychain에 복원 시도 (선택적)
                        try
                        {
                            int storeResult = _SecureStorage_StoreString(key, recoveredValue);
                            if (storeResult == 1)
                            {
                                Debug.Log($"[SecureStorage] [IOS] Keychain에 데이터 복원 완료: {key}");
                            }
                        }
                        catch
                        {
                            // 복원 실패는 무시 (백업 데이터는 사용 가능)
                        }

                        return recoveredValue;
                    }
                }

                return GetStringIOSFallback(key, defaultValue);
            }
        }

        private static void DeleteKeyIOS(string key)
        {
            try
            {
                if (_SecureStorage_IsAvailable() == 1)
                {
                    int result = _SecureStorage_DeleteKey(key);
                    if (result == 1)
                    {
                        if (DEBUG_MODE)
                            Debug.Log($"[SecureStorage] [IOS_KEYCHAIN] Deleted key: {key}");
                    }
                    else
                    {
                        Debug.LogWarning($"[SecureStorage] [IOS] Keychain delete failed for key: {key}");
                    }
                }
                
                // Also delete from fallback storage
                PlayerPrefs.DeleteKey($"SecureStorage_{key}");
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [IOS] Delete failed: {ex.Message}");
                PlayerPrefs.DeleteKey($"SecureStorage_{key}");
                PlayerPrefs.Save();
            }
        }

        private static bool HasKeyIOS(string key)
        {
            try
            {
                if (_SecureStorage_IsAvailable() == 1)
                {
                    int result = _SecureStorage_HasKey(key);
                    if (result == 1)
                    {
                        if (DEBUG_MODE)
                            Debug.Log($"[SecureStorage] [IOS_KEYCHAIN] Key exists '{key}': true");
                        return true;
                    }
                }
                
                // Check fallback storage as well
                bool exists = PlayerPrefs.HasKey($"SecureStorage_{key}");
                if (DEBUG_MODE)
                    Debug.Log($"[SecureStorage] [IOS] Key exists '{key}': {exists} (fallback)");
                return exists;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [IOS] HasKey failed: {ex.Message}");
                return PlayerPrefs.HasKey($"SecureStorage_{key}");
            }
        }

        private static void ClearAllIOS()
        {
            try
            {
                if (_SecureStorage_IsAvailable() == 1)
                {
                    int result = _SecureStorage_ClearAll();
                    if (result == 1)
                    {
                        if (DEBUG_MODE)
                            Debug.Log("[SecureStorage] [IOS_KEYCHAIN] All secure storage cleared");
                    }
                    else
                    {
                        Debug.LogWarning("[SecureStorage] [IOS] Keychain clear failed");
                    }
                }
                
                // Also clear fallback storage for our keys
                // Note: We can't enumerate PlayerPrefs keys, so this is limited
                Debug.Log("[SecureStorage] [IOS] Fallback clear - delete known keys manually");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] [IOS] ClearAll failed: {ex.Message}");
            }
        }
        
        // ========================================
        // iOS Fallback Methods (암호화된 PlayerPrefs)
        // ========================================

        private static void StoreStringIOSFallback(string key, string value)
        {
            string encryptedValue = SimpleEncrypt(value);
            PlayerPrefs.SetString($"SecureStorage_{key}", encryptedValue);
            PlayerPrefs.Save();
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [IOS_FALLBACK] Stored key: {key}");
        }

        private static string GetStringIOSFallback(string key, string defaultValue)
        {
            string encryptedValue = PlayerPrefs.GetString($"SecureStorage_{key}", "");
            if (string.IsNullOrEmpty(encryptedValue))
                return defaultValue;
            
            string decryptedValue = SimpleDecrypt(encryptedValue);
            if (DEBUG_MODE)
                Debug.Log($"[SecureStorage] [IOS_FALLBACK] Retrieved key: {key}");
            return decryptedValue;
        }
#endif

        // ========================================
        // Platform Availability Checks
        // ========================================
        
#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool IsAndroidKeystoreAvailable()
        {
            try
            {
                using (AndroidJavaClass keyStoreClass = new AndroidJavaClass("java.security.KeyStore"))
                {
                    AndroidJavaObject keyStore = keyStoreClass.CallStatic<AndroidJavaObject>("getInstance", ANDROID_KEYSTORE);
                    keyStore.Call("load", (AndroidJavaObject)null);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SecureStorage] [ANDROID] Keystore unavailable: {ex.Message}");
                return false;
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private static bool IsIOSKeychainAvailable()
        {
            try
            {
                return _SecureStorage_IsAvailable() == 1;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SecureStorage] [IOS] Keychain unavailable: {ex.Message}");
                return false;
            }
        }
#endif

        // ========================================
        // 토큰 키 마이그레이션
        // ========================================

        /// <summary>
        /// 레거시 키에서 통합 키로 토큰 마이그레이션
        /// 앱 초기화 시 한 번 호출되어야 함
        /// </summary>
        public static void MigrateLegacyTokenKeys()
        {
            try
            {
                if (DEBUG_MODE)
                    Debug.Log("[SecureStorage] Starting token key migration");

                // Migrate refresh tokens from legacy keys
                foreach (var oldKey in TokenKeys.LegacyRefresh)
                {
                    var value = GetString(oldKey, "");
                    if (!string.IsNullOrEmpty(value))
                    {
                        StoreString(TokenKeys.Refresh, value);
                        DeleteKey(oldKey);
                        if (DEBUG_MODE)
                            Debug.Log($"[SecureStorage] Migrated refresh token from {oldKey} to {TokenKeys.Refresh}");
                    }
                }

                // Migrate access tokens from legacy keys
                foreach (var oldKey in TokenKeys.LegacyAccess)
                {
                    var value = GetString(oldKey, "");
                    if (!string.IsNullOrEmpty(value))
                    {
                        StoreString(TokenKeys.Access, value);
                        DeleteKey(oldKey);
                        if (DEBUG_MODE)
                            Debug.Log($"[SecureStorage] Migrated access token from {oldKey} to {TokenKeys.Access}");
                    }
                }

                // Also check PlayerPrefs for very old tokens (pre-SecureStorage)
                var ppRefresh = PlayerPrefs.GetString("blokus_refresh_token", "");
                if (!string.IsNullOrEmpty(ppRefresh))
                {
                    StoreString(TokenKeys.Refresh, ppRefresh);
                    PlayerPrefs.DeleteKey("blokus_refresh_token");
                    if (DEBUG_MODE)
                        Debug.Log("[SecureStorage] Migrated refresh token from PlayerPrefs");
                }

                var ppAccess = PlayerPrefs.GetString("blokus_access_token", "");
                if (!string.IsNullOrEmpty(ppAccess))
                {
                    StoreString(TokenKeys.Access, ppAccess);
                    PlayerPrefs.DeleteKey("blokus_access_token");
                    if (DEBUG_MODE)
                        Debug.Log("[SecureStorage] Migrated access token from PlayerPrefs");
                }
                
                PlayerPrefs.Save();

                if (DEBUG_MODE)
                    Debug.Log("[SecureStorage] Token key migration completed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] Token migration failed: {ex.Message}");
            }
        }

        // ========================================
        // 향상된 오류 복구
        // ========================================

        private static bool TrySecureOperation(string operation, string key, System.Func<bool> secureAction, System.Action fallbackAction)
        {
            try
            {
                if (secureAction())
                {
                    return true;
                }
                else
                {
                    Debug.LogWarning($"[SecureStorage] {operation} failed for key '{key}', using fallback");
                    fallbackAction?.Invoke();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] {operation} exception for key '{key}': {ex.Message}");
                try
                {
                    fallbackAction?.Invoke();
                }
                catch (Exception fallbackEx)
                {
                    Debug.LogError($"[SecureStorage] {operation} fallback failed: {fallbackEx.Message}");
                }
                return false;
            }
        }
        
        /// <summary>
        /// 복호화를 시도하여 저장된 데이터 무결성 검증
        /// </summary>
        public static bool ValidateStoredData(string key)
        {
            try
            {
                string testValue = GetString(key, "");
                return !string.IsNullOrEmpty(testValue);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] 데이터 검증 실패 '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 키를 삭제하여 손상된 데이터 복구
        /// </summary>
        public static void RepairCorruptedData(string key)
        {
            try
            {
                Debug.LogWarning($"[SecureStorage] 손상된 데이터 복구 중: {key}");
                DeleteKey(key);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SecureStorage] 데이터 복구 실패 '{key}': {ex.Message}");
            }
        }

        // ========================================
        // Editor/Fallback용 간단한 암호화
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