import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:relieflink_mobile/api/api_client.dart';
import 'package:relieflink_mobile/api/models.dart';
import 'package:relieflink_mobile/app.dart';
import 'package:relieflink_mobile/data/token_store.dart';
import 'package:relieflink_mobile/location/location_service.dart';
import 'package:relieflink_mobile/state/session.dart';

final fixedNow = DateTime.utc(2026, 10, 13, 12);

Map<String, dynamic> requestJson({
  String id = 'r1',
  String status = 'Submitted',
  String urgency = 'High',
  String district = 'Gampaha',
  String? cancellationReason,
  String? neededBy,
}) =>
    {
      'id': id, 'householdSize': 5, 'elderlyCount': 1, 'childrenCount': 2, 'homeFlooded': true,
      'district': district, 'addressText': '12 Temple Road, Kelaniya', 'latitude': 6.95, 'longitude': 79.92,
      'notes': 'Water is rising', 'neededBy': neededBy, 'status': status, 'triageScore': 55, 'urgency': urgency,
      'cancellationReason': cancellationReason,
      'items': [
        {'id': 'i1', 'category': 'Water', 'quantity': 20, 'description': null},
        {'id': 'i2', 'category': 'Food', 'quantity': 5, 'description': 'dry rations'},
      ],
      'createdAt': '2026-10-13T09:00:00Z',
    };

Map<String, dynamic> authJson({String role = 'Citizen'}) => {
      'token': 'tok-123',
      'expiresAtUtc': '2999-01-01T00:00:00Z',
      'user': {'id': 'u1', 'fullName': 'Nimali Perera', 'email': 'n@x.lk', 'role': role, 'phone': null, 'district': null},
    };

Map<String, dynamic> pagedJson(List<Map<String, dynamic>> items) =>
    {'items': items, 'page': 1, 'pageSize': 30, 'totalCount': items.length, 'totalPages': 1};

class Recorded {
  Recorded(this.method, this.path, this.query, this.body, this.headers);
  final String method;
  final String path;
  final Map<String, String> query;
  final Object? body;
  final Map<String, String> headers;
}

/// A fake server. Routes are "METHOD /path" -> a JSON value, or a function returning http.Response.
class FakeServer {
  FakeServer(this.routes);

  final Map<String, Object? Function(Recorded)> routes;
  final List<Recorded> calls = [];

  MockClient get client => MockClient((request) async {
        final body = request.body.isEmpty ? null : jsonDecode(request.body);
        final recorded = Recorded(request.method, request.url.path, request.url.queryParameters, body, request.headers);
        calls.add(recorded);
        final route = routes['${request.method} ${request.url.path}'];
        if (route == null) return http.Response(jsonEncode({'title': 'No fake for ${request.method} ${request.url.path}'}), 404);
        final value = route(recorded);
        if (value is http.Response) return value;
        return http.Response(jsonEncode(value), 200, headers: {'content-type': 'application/json'});
      });
}

http.Response problem(int status, String title, [Map<String, List<String>>? errors]) =>
    http.Response(jsonEncode({'title': title, if (errors != null) 'errors': errors}), status);

class FakeLocation implements LocationService {
  FakeLocation({this.point, this.failure});
  final ({double latitude, double longitude})? point;
  final String? failure;

  @override
  Future<({double latitude, double longitude})> current() async {
    if (failure != null) throw LocationUnavailable(failure!);
    return point ?? (latitude: 6.58, longitude: 79.96);
  }
}

class TestApp {
  TestApp._(this.server, this.api, this.session, this.store, this.widget);

  final FakeServer server;
  final ApiClient api;
  final SessionController session;
  final MemoryTokenStore store;
  final ReliefLinkApp widget;

  static Future<TestApp> create(
    Map<String, Object? Function(Recorded)> routes, {
    bool signedIn = true,
    String role = 'Citizen',
    LocationService? location,
  }) async {
    final server = FakeServer(routes);
    final api = ApiClient(baseUrl: 'http://api.test', client: server.client);
    final store = MemoryTokenStore();
    if (signedIn) {
      store.saved = SavedSession(
        token: 'tok-123',
        expiresAt: DateTime.utc(2999),
        user: AppUser.fromJson(authJson(role: role)['user'] as Map<String, dynamic>),
      );
    }
    final session = SessionController(api: api, store: store, clock: () => fixedNow);
    await session.restore();
    return TestApp._(server, api, session, store,
        ReliefLinkApp(session: session, api: api, location: location ?? FakeLocation(), clock: () => fixedNow));
  }
}
