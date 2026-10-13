import 'package:flutter/foundation.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../data/token_store.dart';

/// Who is signed in. Screens listen to this and rebuild when it changes.
class SessionController extends ChangeNotifier {
  SessionController({required this.api, required this.store, DateTime Function()? clock}) : _now = clock ?? DateTime.now {
    api.onUnauthorized = () => signOut();
  }

  final ApiClient api;
  final TokenStore store;
  final DateTime Function() _now;

  AppUser? _user;
  bool _ready = false;

  AppUser? get user => _user;

  /// False until we have looked for a saved session at start-up.
  bool get ready => _ready;

  Future<void> restore() async {
    final saved = await store.read();
    if (saved != null && !saved.isExpired(_now())) {
      api.setToken(saved.token);
      _user = saved.user;
    } else if (saved != null) {
      await store.clear();
    }
    _ready = true;
    notifyListeners();
  }

  Future<void> signIn(String email, String password) => _accept(api.login(email.trim(), password));

  Future<void> register({
    required String fullName,
    required String email,
    required String password,
    String? phone,
    String? district,
  }) =>
      _accept(api.register(fullName: fullName.trim(), email: email.trim(), password: password, phone: phone?.trim(), district: district));

  Future<void> _accept(Future<AuthResult> call) async {
    final result = await call;
    if (result.user.role == 'Coordinator' || result.user.role == 'Admin') {
      // Never keep a staff token on a phone app that is not built for staff.
      throw ApiException(403, 'This app is for citizens and volunteers. Coordinators use the web console.');
    }
    api.setToken(result.token);
    await store.write(SavedSession(token: result.token, expiresAt: result.expiresAt, user: result.user));
    _user = result.user;
    notifyListeners();
  }

  Future<void> signOut() async {
    api.setToken(null);
    await store.clear();
    if (_user != null) {
      _user = null;
      notifyListeners();
    }
  }
}
