// Walks the citizen's journey against a REAL running API (no mocks).
//
//   dart run tool/live_check.dart --api http://localhost:5039
//
// It registers a throw-away citizen, sends a request, changes it, reads it back, cancels it,
// and checks that the API's rules match what the app assumes. It uses only the app's own ApiClient.
import 'dart:io';
import 'dart:math';

import 'package:relieflink_mobile/api/api_client.dart';
import 'package:relieflink_mobile/data/districts.dart';
import 'package:relieflink_mobile/logic/request_form.dart';

void check(bool ok, String what) {
  stdout.writeln('${ok ? 'PASS' : 'FAIL'}  $what');
  if (!ok) exitCode = 1;
}

Future<void> main(List<String> args) async {
  final i = args.indexOf('--api');
  final base = i >= 0 ? args[i + 1] : 'http://localhost:5039';
  final api = ApiClient(baseUrl: base);

  final stamp = Random().nextInt(1 << 30);
  final email = 'live$stamp@example.com';
  final password = 'Live${stamp}x';

  final auth = await api.register(fullName: 'Live Check', email: email, password: password, district: 'Galle');
  api.setToken(auth.token);
  check(auth.user.role == 'Citizen', 'register creates a Citizen');

  final draft = RequestDraft(
    householdSize: 4, elderlyCount: 1, childrenCount: 1, homeFlooded: true, district: 'Galle',
    addressText: '48 Fort Road, Galle', notes: 'Live check', needs: {'Water': 6, 'Food': 4},
    neededBy: DateTime.now().toUtc().add(const Duration(hours: 12)),
  );
  check(validateDraft(draft, DateTime.now().toUtc()).isEmpty, 'the app accepts the draft before sending');

  final created = await api.createRequest(draft.toJson());
  check(created.status == 'Submitted', 'a new request starts as Submitted');
  check(created.items.length == 2, 'both needs were saved');
  check(created.latitude == districtByName('Galle')!.latitude, 'the district centre was used as the location');
  check(created.canEdit && created.canCancel, 'a Submitted request can be edited and cancelled');

  final page = await api.myRequests();
  check(page.items.any((r) => r.id == created.id), 'the new request is in my list');

  draft.householdSize = 5;
  draft.needs = {'Water': 8};
  final changed = await api.updateRequest(created.id, draft.toJson());
  check(changed.householdSize == 5 && changed.items.single.quantity == 8, 'editing works while Submitted');

  // Everything the app validates is also enforced by the API. Send bad data directly.
  final bad = RequestDraft(householdSize: 4, district: 'Galle', addressText: '48 Fort Road', needs: {'Water': 1})
    ..latitude = 51.5
    ..longitude = -0.12;
  try {
    await api.createRequest(bad.toJson());
    check(false, 'the API refuses a location outside Sri Lanka');
  } on ApiException catch (e) {
    check(e.status == 400, 'the API refuses a location outside Sri Lanka (${e.status})');
  }

  final cancelled = await api.cancelRequest(created.id, 'Live check finished');
  check(cancelled.status == 'Cancelled' && cancelled.cancellationReason == 'Live check finished', 'cancel works and keeps the reason');
  check(!cancelled.canEdit && !cancelled.canCancel, 'a Cancelled request cannot be edited or cancelled again');

  try {
    await api.cancelRequest(created.id, null);
    check(false, 'cancelling twice is refused');
  } on ApiException catch (e) {
    check(e.status == 409, 'cancelling twice is refused with 409 (${e.status})');
  }

  // Another citizen cannot see this one.
  final other = ApiClient(baseUrl: base);
  final otherAuth = await other.register(fullName: 'Other Person', email: 'other$stamp@example.com', password: password);
  other.setToken(otherAuth.token);
  try {
    await other.request(created.id);
    check(false, 'another citizen cannot read my request');
  } on ApiException catch (e) {
    check(e.status == 404, 'another citizen gets 404 for my request (${e.status})');
  }
  check((await other.myRequests()).items.isEmpty, 'another citizen sees an empty list');

  stdout.writeln(exitCode == 0 ? '\nAll live checks passed.' : '\nSome live checks FAILED.');
}
