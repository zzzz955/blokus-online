#import <Foundation/Foundation.h>
#import <Security/Security.h>

extern "C" {
    
// SecureStorage iOS Native Implementation
// Keychain Services API를 사용한 보안 토큰 저장

NSString* getKeychainKey(const char* key) {
    return [NSString stringWithFormat:@"blokus.%@", [NSString stringWithUTF8String:key]];
}

NSMutableDictionary* createKeychainQuery(NSString* service, NSString* account) {
    return [[NSMutableDictionary alloc] initWithDictionary:@{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: service,
        (__bridge id)kSecAttrAccount: account,
        (__bridge id)kSecAttrAccessible: (__bridge id)kSecAttrAccessibleWhenUnlockedThisDeviceOnly
    }];
}

// Store string securely in iOS Keychain
int _SecureStorage_StoreString(const char* key, const char* value) {
    @try {
        NSString* nsKey = getKeychainKey(key);
        NSString* nsValue = [NSString stringWithUTF8String:value];
        
        if (!nsKey || !nsValue) {
            NSLog(@"[SecureStorage] Invalid key or value");
            return 0;
        }
        
        NSData* valueData = [nsValue dataUsingEncoding:NSUTF8StringEncoding];
        
        // Delete existing item first
        NSMutableDictionary* deleteQuery = createKeychainQuery(@"BlokusSecureStorage", nsKey);
        SecItemDelete((__bridge CFDictionaryRef)deleteQuery);
        
        // Add new item
        NSMutableDictionary* addQuery = createKeychainQuery(@"BlokusSecureStorage", nsKey);
        [addQuery setObject:valueData forKey:(__bridge id)kSecValueData];
        
        OSStatus status = SecItemAdd((__bridge CFDictionaryRef)addQuery, NULL);
        
        if (status == errSecSuccess) {
            NSLog(@"[SecureStorage] Successfully stored key: %@", nsKey);
            return 1;
        } else {
            NSLog(@"[SecureStorage] Failed to store key: %@, status: %d", nsKey, (int)status);
            return 0;
        }
    }
    @catch (NSException *exception) {
        NSLog(@"[SecureStorage] Exception in StoreString: %@", exception.reason);
        return 0;
    }
}

// Retrieve string securely from iOS Keychain  
char* _SecureStorage_GetString(const char* key, const char* defaultValue) {
    @try {
        NSString* nsKey = getKeychainKey(key);
        
        if (!nsKey) {
            NSLog(@"[SecureStorage] Invalid key");
            return strdup(defaultValue ? defaultValue : "");
        }
        
        NSMutableDictionary* query = createKeychainQuery(@"BlokusSecureStorage", nsKey);
        [query setObject:(__bridge id)kSecMatchLimitOne forKey:(__bridge id)kSecMatchLimit];
        [query setObject:(__bridge id)kCFBooleanTrue forKey:(__bridge id)kSecReturnData];
        
        CFDataRef dataRef = NULL;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, (CFTypeRef*)&dataRef);
        
        if (status == errSecSuccess && dataRef != NULL) {
            NSData* data = (__bridge NSData*)dataRef;
            NSString* value = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
            
            if (value) {
                NSLog(@"[SecureStorage] Successfully retrieved key: %@", nsKey);
                char* result = strdup([value UTF8String]);
                CFRelease(dataRef);
                return result;
            } else {
                NSLog(@"[SecureStorage] Failed to decode value for key: %@", nsKey);
                CFRelease(dataRef);
                return strdup(defaultValue ? defaultValue : "");
            }
        } else {
            if (status == errSecItemNotFound) {
                NSLog(@"[SecureStorage] Key not found: %@", nsKey);
            } else {
                NSLog(@"[SecureStorage] Failed to retrieve key: %@, status: %d", nsKey, (int)status);
            }
            return strdup(defaultValue ? defaultValue : "");
        }
    }
    @catch (NSException *exception) {
        NSLog(@"[SecureStorage] Exception in GetString: %@", exception.reason);
        return strdup(defaultValue ? defaultValue : "");
    }
}

// Delete string from iOS Keychain
int _SecureStorage_DeleteKey(const char* key) {
    @try {
        NSString* nsKey = getKeychainKey(key);
        
        if (!nsKey) {
            NSLog(@"[SecureStorage] Invalid key");
            return 0;
        }
        
        NSMutableDictionary* deleteQuery = createKeychainQuery(@"BlokusSecureStorage", nsKey);
        OSStatus status = SecItemDelete((__bridge CFDictionaryRef)deleteQuery);
        
        if (status == errSecSuccess || status == errSecItemNotFound) {
            NSLog(@"[SecureStorage] Successfully deleted key: %@", nsKey);
            return 1;
        } else {
            NSLog(@"[SecureStorage] Failed to delete key: %@, status: %d", nsKey, (int)status);
            return 0;
        }
    }
    @catch (NSException *exception) {
        NSLog(@"[SecureStorage] Exception in DeleteKey: %@", exception.reason);
        return 0;
    }
}

// Check if key exists in iOS Keychain
int _SecureStorage_HasKey(const char* key) {
    @try {
        NSString* nsKey = getKeychainKey(key);
        
        if (!nsKey) {
            NSLog(@"[SecureStorage] Invalid key");
            return 0;
        }
        
        NSMutableDictionary* query = createKeychainQuery(@"BlokusSecureStorage", nsKey);
        [query setObject:(__bridge id)kSecMatchLimitOne forKey:(__bridge id)kSecMatchLimit];
        
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, NULL);
        
        if (status == errSecSuccess) {
            NSLog(@"[SecureStorage] Key exists: %@", nsKey);
            return 1;
        } else {
            if (status != errSecItemNotFound) {
                NSLog(@"[SecureStorage] Error checking key existence: %@, status: %d", nsKey, (int)status);
            }
            return 0;
        }
    }
    @catch (NSException *exception) {
        NSLog(@"[SecureStorage] Exception in HasKey: %@", exception.reason);
        return 0;
    }
}

// Clear all Blokus secure storage entries
int _SecureStorage_ClearAll() {
    @try {
        NSMutableDictionary* deleteQuery = [[NSMutableDictionary alloc] initWithDictionary:@{
            (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService: @"BlokusSecureStorage"
        }];
        
        OSStatus status = SecItemDelete((__bridge CFDictionaryRef)deleteQuery);
        
        if (status == errSecSuccess || status == errSecItemNotFound) {
            NSLog(@"[SecureStorage] Successfully cleared all secure storage");
            return 1;
        } else {
            NSLog(@"[SecureStorage] Failed to clear all secure storage, status: %d", (int)status);
            return 0;
        }
    }
    @catch (NSException *exception) {
        NSLog(@"[SecureStorage] Exception in ClearAll: %@", exception.reason);
        return 0;
    }
}

// Get Keychain availability status
int _SecureStorage_IsAvailable() {
    @try {
        // Test Keychain availability by attempting to create a query
        NSMutableDictionary* testQuery = [[NSMutableDictionary alloc] initWithDictionary:@{
            (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService: @"BlokusSecureStorageTest",
            (__bridge id)kSecAttrAccount: @"availability_test"
        }];
        
        // Try to query (this will fail if Keychain is locked/unavailable)
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)testQuery, NULL);
        
        // errSecItemNotFound means Keychain is available but item doesn't exist (expected)
        // errSecSuccess means somehow test item exists (also means available)
        // Any other error might indicate Keychain unavailability
        if (status == errSecItemNotFound || status == errSecSuccess) {
            return 1;
        } else {
            NSLog(@"[SecureStorage] Keychain may not be available, status: %d", (int)status);
            return 0;
        }
    }
    @catch (NSException *exception) {
        NSLog(@"[SecureStorage] Exception checking Keychain availability: %@", exception.reason);
        return 0;
    }
}

}