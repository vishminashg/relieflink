
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:relieflink_mobile/api/api_client.dart';
import 'package:relieflink_mobile/api/models.dart';
import 'package:relieflink_mobile/data/token_store.dart';
import 'package:relieflink_mobile/state/session.dart';

import 'support/fakes.dart';

ApiClient clientFor(FakeServer s) => ApiClient(baseUrl: 'http://api.test', client: s.client);

void main() {
  group('ApiClient', () {
    test('sends the bearer token and a JSON body', () async {
      final server = FakeServer({'POST /api/relief-requests': (_) => requestJson()});
      final api = clientFor(server)..setToken('abc');
      await api.createRequest({'householdSize': 2});
      final call = server.calls.single;
      expect(call.headers['Authorization'], 'Bearer abc');
      expect(call.headers['Content-Type'], contains('application/json'));
      expect((call.body as Map)['householdSize'], 2);
    });

    test('sends no Authorization header before sign in', () async {
      final server = FakeServer({'POST /api/auth/login': (_) => authJson()});
      await clientFor(server).login('a@b.lk', 'pw');
      expect(server.calls.single.headers.containsKey('Authorization'), isFalse);
    });

    test('lists my requests newest first', () async {
      final server = FakeServer({'GET /api/relief-requests': (_) => pagedJson([requestJson()])});
      final page = await clientFor(server).myRequests();
      expect(page.items.single.id, 'r1');
      expect(server.calls.single.query['sort'], 'newest');
    });

    test('loads nearby shelters with the request coordinates', () async {
      final server = FakeServer({'GET /api/shelters/nearest': (_) => [
        {'id': 's1', 'name': 'Kalutara Hall', 'district': 'Kalutara', 'address': 'Main Road', 'latitude': 6.58, 'longitude': 79.96, 'capacity': 100, 'occupied': 40, 'available': 60, 'isActive': true},
      ]});
      final shelters = await clientFor(server).nearestShelters(latitude: 6.58, longitude: 79.96);
      expect(shelters.single.available, 60);
      expect(server.calls.single.query['latitude'], '6.58');
      expect(server.calls.single.query['longitude'], '79.96');
    });

    test('register always asks for the Citizen role', () async {
      final server = FakeServer({'POST /api/auth/register': (_) => authJson()});
      await clientFor(server).register(fullName: 'A B', email: 'a@b.lk', password: 'abcdefg1', phone: '', district: 'Galle');
      final body = server.calls.single.body as Map;
      expect(body['role'], 'Citizen');
      expect(body['phone'], isNull);
    });

    test('cancel sends null for a blank reason', () async {
      final server = FakeServer({'POST /api/relief-requests/r1/cancel': (_) => requestJson(status: 'Cancelled')});
      await clientFor(server).cancelRequest('r1', '   ');
      expect((server.calls.single.body as Map)['reason'], isNull);
    });

    test('turns a problem-details answer into a readable error with field messages', () async {
      final server = FakeServer({
        'POST /api/relief-requests': (_) => problem(400, 'One or more validation errors occurred.', {'Items': ['Add at least one need.']}),
      });
      final error = await clientFor(server).createRequest({}).then<ApiException?>((_) => null).catchError((Object e) => e as ApiException);
      expect(error!.status, 400);
      expect(error.message, 'One or more validation errors occurred.');
      expect(error.fields['Items'], ['Add at least one need.']);
    });

    test('a failed login keeps the server message; a 401 while signed in says the session ended', () async {
      final server = FakeServer({
        'POST /api/auth/login': (_) => problem(401, 'Invalid email or password.'),
        'GET /api/relief-requests': (_) => problem(401, 'x'),
      });
      final api = clientFor(server);
      await expectLater(api.login('a@b.lk', 'bad'), throwsA(isA<ApiException>().having((e) => e.message, 'message', 'Invalid email or password.')));
      var ended = 0;
      api.onUnauthorized = () => ended++;
      api.setToken('abc');
      await expectLater(api.myRequests(), throwsA(isA<ApiException>().having((e) => e.message, 'message', contains('session has ended'))));
      expect(ended, 1);
    });

    test('the unauthorized handler does not fire for a failed login', () async {
      final server = FakeServer({'POST /api/auth/login': (_) => problem(401, 'Invalid email or password.')});
      final api = clientFor(server);
      var ended = 0;
      api.onUnauthorized = () => ended++;
      await expectLater(api.login('a@b.lk', 'bad'), throwsA(isA<ApiException>()));
      expect(ended, 0);
    });

    test('403 and network failure and timeout have plain messages', () async {
      final forbidden = clientFor(FakeServer({'GET /api/relief-requests': (_) => problem(403, 'x')}));
      await expectLater(forbidden.myRequests(), throwsA(isA<ApiException>().having((e) => e.message, 'm', contains('not allowed'))));

      final offline = ApiClient(baseUrl: 'http://api.test', client: MockClient((_) async => throw http.ClientException('no route')));
      await expectLater(offline.myRequests(), throwsA(isA<ApiException>().having((e) => e.status, 'status', 0)));

      final slow = ApiClient(
        baseUrl: 'http://api.test',
        timeout: const Duration(milliseconds: 20),
        client: MockClient((_) async { await Future<void>.delayed(const Duration(milliseconds: 200)); return http.Response('{}', 200); }),
      );
      await expectLater(slow.myRequests(), throwsA(isA<ApiException>().having((e) => e.message, 'm', contains('too long'))));
    });

    test('a non-JSON error body still gives a generic message', () async {
      final api = ApiClient(baseUrl: 'http://api.test', client: MockClient((_) async => http.Response('<html>Bad gateway</html>', 502)));
      await expectLater(api.myRequests(), throwsA(isA<ApiException>().having((e) => e.message, 'm', contains('502'))));
    });
  });

  group('SessionController', () {
    Future<SessionController> make(FakeServer server, MemoryTokenStore store) async {
      final c = SessionController(api: clientFor(server), store: store, clock: () => fixedNow);
      await c.restore();
      return c;
    }

    test('signs a citizen in and stores the session', () async {
      final store = MemoryTokenStore();
      final c = await make(FakeServer({'POST /api/auth/login': (_) => authJson()}), store);
      await c.signIn('  n@x.lk ', 'pw');
      expect(c.user!.fullName, 'Nimali Perera');
      expect(store.saved!.token, 'tok-123');
    });

    test('volunteers may sign in', () async {
      final c = await make(FakeServer({'POST /api/auth/login': (_) => authJson(role: 'Volunteer')}), MemoryTokenStore());
      await c.signIn('v@x.lk', 'pw');
      expect(c.user!.role, 'Volunteer');
    });

    for (final role in ['Coordinator', 'Admin']) {
      test('refuses $role and keeps no token', () async {
        final store = MemoryTokenStore();
        final c = await make(FakeServer({'POST /api/auth/login': (_) => authJson(role: role)}), store);
        await expectLater(c.signIn('s@x.lk', 'pw'), throwsA(isA<ApiException>().having((e) => e.message, 'm', contains('web console'))));
        expect(c.user, isNull);
        expect(store.saved, isNull);
      });
    }

    test('restores a saved session and uses its token', () async {
      final store = MemoryTokenStore();
      final first = await make(FakeServer({'POST /api/auth/login': (_) => authJson()}), store);
      await first.signIn('n@x.lk', 'pw');

      final server = FakeServer({'GET /api/relief-requests': (_) => pagedJson([])});
      final second = await make(server, store);
      expect(second.user, isNotNull);
      await second.api.myRequests();
      expect(server.calls.single.headers['Authorization'], 'Bearer tok-123');
    });

    test('ignores and clears an expired session', () async {
      final store = MemoryTokenStore()
        ..saved = SavedSession(
          token: 'old',
          expiresAt: fixedNow.subtract(const Duration(minutes: 1)),
          user: const AppUser(id: 'u1', fullName: 'Old Session', email: 'o@x.lk', role: 'Citizen'),
        );
      final c = await make(FakeServer({}), store);
      expect(c.user, isNull);
      expect(store.saved, isNull);
    });

    test('sign out clears everything, and a 401 from the API signs out', () async {
      final store = MemoryTokenStore();
      final server = FakeServer({'POST /api/auth/login': (_) => authJson(), 'GET /api/relief-requests': (_) => problem(401, 'x')});
      final c = await make(server, store);
      await c.signIn('n@x.lk', 'pw');
      await expectLater(c.api.myRequests(), throwsA(isA<ApiException>()));
      await Future<void>.delayed(Duration.zero);
      expect(c.user, isNull);
      expect(store.saved, isNull);
    });
  });
}
