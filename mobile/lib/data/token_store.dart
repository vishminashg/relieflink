import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../api/models.dart';

/// What we keep between app launches: the sign-in token and who it belongs to.
class SavedSession {
  const SavedSession({required this.token, required this.expiresAt, required this.user});

  final String token;
  final DateTime expiresAt;
  final AppUser user;

  bool isExpired(DateTime now) => !expiresAt.isAfter(now);
}

abstract class TokenStore {
  Future<SavedSession?> read();
  Future<void> write(SavedSession session);
  Future<void> clear();
}

/// Keeps the session in Android's encrypted storage (Keystore).
class SecureTokenStore implements TokenStore {
  SecureTokenStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  static const _key = 'relieflink.session';
  final FlutterSecureStorage _storage;

  @override
  Future<SavedSession?> read() async {
    try {
      final raw = await _storage.read(key: _key);
      if (raw == null) return null;
      final json = jsonDecode(raw) as Map<String, dynamic>;
      return SavedSession(
        token: json['token'] as String,
        expiresAt: DateTime.parse(json['expiresAt'] as String),
        user: AppUser.fromJson(json['user'] as Map<String, dynamic>),
      );
    } catch (_) {
      return null; // unreadable data is treated as "signed out"
    }
  }

  @override
  Future<void> write(SavedSession session) => _storage.write(
        key: _key,
        value: jsonEncode({
          'token': session.token,
          'expiresAt': session.expiresAt.toIso8601String(),
          'user': session.user.toJson(),
        }),
      );

  @override
  Future<void> clear() => _storage.delete(key: _key);
}

/// For tests.
class MemoryTokenStore implements TokenStore {
  SavedSession? saved;

  @override
  Future<SavedSession?> read() async => saved;

  @override
  Future<void> write(SavedSession session) async => saved = session;

  @override
  Future<void> clear() async => saved = null;
}
