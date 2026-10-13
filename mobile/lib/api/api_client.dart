import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import 'models.dart';

/// A readable error built from the API's problem-details answer.
class ApiException implements Exception {
  ApiException(this.status, this.message, [this.fields = const {}]);

  /// HTTP status, or 0 when the server could not be reached at all.
  final int status;
  final String message;

  /// Field name -> messages, when the API rejected specific fields.
  final Map<String, List<String>> fields;

  @override
  String toString() => message;
}

/// Talks to the ReliefLink API. Knows nothing about screens.
class ApiClient {
  ApiClient({required this.baseUrl, http.Client? client, this.timeout = const Duration(seconds: 20)})
      : _http = client ?? http.Client();

  final String baseUrl;
  final Duration timeout;
  final http.Client _http;

  String? _token;
  void Function()? onUnauthorized;

  void setToken(String? token) => _token = token;

  Future<dynamic> _send(String method, String path, {Object? body, Map<String, String>? query}) async {
    final uri = Uri.parse('$baseUrl$path').replace(queryParameters: query);
    final headers = <String, String>{
      'Accept': 'application/json',
      if (body != null) 'Content-Type': 'application/json',
      if (_token != null) 'Authorization': 'Bearer $_token',
    };

    http.Response response;
    try {
      final request = http.Request(method, uri)..headers.addAll(headers);
      if (body != null) request.body = jsonEncode(body);
      response = await http.Response.fromStream(await _http.send(request).timeout(timeout));
    } on TimeoutException {
      throw ApiException(0, 'The server took too long to answer. Check your signal and try again.');
    } catch (_) {
      throw ApiException(0, 'Cannot reach the server. Check your internet connection and try again.');
    }

    final hadToken = _token != null;
    if (response.statusCode == 401 && hadToken) onUnauthorized?.call();

    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.body.isEmpty) return null;
      return jsonDecode(utf8.decode(response.bodyBytes));
    }

    var message = 'The server answered ${response.statusCode}. Try again later.';
    var fields = <String, List<String>>{};
    try {
      final problem = jsonDecode(utf8.decode(response.bodyBytes));
      if (problem is Map<String, dynamic>) {
        if (problem['title'] is String) message = problem['title'] as String;
        if (problem['errors'] is Map) {
          fields = (problem['errors'] as Map).map(
            (k, v) => MapEntry(k as String, (v as List).map((e) => e.toString()).toList()),
          );
        }
      }
    } catch (_) {
      // Not JSON: keep the generic message.
    }
    if (response.statusCode == 401 && hadToken) message = 'Your session has ended. Please sign in again.';
    if (response.statusCode == 403) message = 'Your account is not allowed to do this.';
    throw ApiException(response.statusCode, message, fields);
  }

  Future<AuthResult> login(String email, String password) async =>
      AuthResult.fromJson(await _send('POST', '/api/auth/login', body: {'email': email, 'password': password}) as Map<String, dynamic>);

  Future<AuthResult> register({
    required String fullName,
    required String email,
    required String password,
    String? phone,
    String? district,
  }) async =>
      AuthResult.fromJson(await _send('POST', '/api/auth/register', body: {
        'fullName': fullName,
        'email': email,
        'password': password,
        'phone': (phone == null || phone.isEmpty) ? null : phone,
        'district': district,
        'role': 'Citizen',
      }) as Map<String, dynamic>);

  Future<PagedRequests> myRequests({int page = 1, int pageSize = 30}) async => PagedRequests.fromJson(
      await _send('GET', '/api/relief-requests', query: {'sort': 'newest', 'page': '$page', 'pageSize': '$pageSize'}) as Map<String, dynamic>);

  Future<ReliefRequest> request(String id) async =>
      ReliefRequest.fromJson(await _send('GET', '/api/relief-requests/$id') as Map<String, dynamic>);

  Future<ReliefRequest> createRequest(Map<String, dynamic> body) async =>
      ReliefRequest.fromJson(await _send('POST', '/api/relief-requests', body: body) as Map<String, dynamic>);

  Future<ReliefRequest> updateRequest(String id, Map<String, dynamic> body) async =>
      ReliefRequest.fromJson(await _send('PUT', '/api/relief-requests/$id', body: body) as Map<String, dynamic>);

  Future<ReliefRequest> cancelRequest(String id, String? reason) async => ReliefRequest.fromJson(await _send(
      'POST', '/api/relief-requests/$id/cancel',
      body: {'reason': (reason == null || reason.trim().isEmpty) ? null : reason.trim()}) as Map<String, dynamic>);
}
