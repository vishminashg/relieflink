import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:relieflink_mobile/data/districts.dart';
import 'package:relieflink_mobile/logic/request_form.dart';

import 'support/fakes.dart';

Future<void> pump(WidgetTester tester, TestApp app) async {
  tester.view.physicalSize = const Size(900, 7000); // tall, so the whole form is built
  tester.view.devicePixelRatio = 2;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(app.widget);
  await tester.pumpAndSettle();
}

Future<void> tapText(WidgetTester tester, String text) async {
  final finder = find.text(text);
  await tester.ensureVisible(finder);
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

void main() {
  group('Sign in', () {
    testWidgets('signed-out people see the sign in screen', (tester) async {
      await pump(tester, await TestApp.create({}, signedIn: false));
      expect(find.text('Sign in'), findsWidgets);
      expect(find.text('Create an account'), findsOneWidget);
    });

    testWidgets('does not call the API when fields are empty', (tester) async {
      final app = await TestApp.create({}, signedIn: false);
      await pump(tester, app);
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();
      expect(find.text('Enter your email address.'), findsOneWidget);
      expect(find.text('Enter your password.'), findsOneWidget);
      expect(app.server.calls, isEmpty);
    });

    testWidgets('a citizen signs in and sees their requests', (tester) async {
      final app = await TestApp.create({
        'POST /api/auth/login': (_) => authJson(),
        'GET /api/relief-requests': (_) => pagedJson([requestJson()]),
      }, signedIn: false);
      await pump(tester, app);
      await tester.enterText(find.byKey(const Key('email')), 'n@x.lk');
      await tester.enterText(find.byKey(const Key('password')), 'Passw0rd!');
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();
      expect(find.text('My requests'), findsOneWidget);
      expect(find.text('Hello, Nimali'), findsOneWidget);
      expect(find.text('Gampaha'), findsOneWidget);
    });

    testWidgets('staff are refused with a message and stay on the sign in screen', (tester) async {
      final app = await TestApp.create({'POST /api/auth/login': (_) => authJson(role: 'Coordinator')}, signedIn: false);
      await pump(tester, app);
      await tester.enterText(find.byKey(const Key('email')), 's@x.lk');
      await tester.enterText(find.byKey(const Key('password')), 'Passw0rd!');
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Coordinators use the web console'), findsOneWidget);
      expect(app.store.saved, isNull);
      expect(find.text('My requests'), findsNothing);
    });

    testWidgets('a wrong password shows the server message', (tester) async {
      final app = await TestApp.create({'POST /api/auth/login': (_) => problem(401, 'Invalid email or password.')}, signedIn: false);
      await pump(tester, app);
      await tester.enterText(find.byKey(const Key('email')), 'n@x.lk');
      await tester.enterText(find.byKey(const Key('password')), 'wrong');
      await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
      await tester.pumpAndSettle();
      expect(find.text('Invalid email or password.'), findsOneWidget);
    });

    testWidgets('volunteers see their own placeholder, not the citizen list', (tester) async {
      await pump(tester, await TestApp.create({}, role: 'Volunteer'));
      expect(find.textContaining('Volunteer tasks will appear here'), findsOneWidget);
      expect(find.text('Ask for help'), findsNothing);
    });

    testWidgets('registering checks the password rules before calling the API', (tester) async {
      final app = await TestApp.create({}, signedIn: false);
      await pump(tester, app);
      await tapText(tester, 'Create an account');
      await tester.enterText(find.byKey(const Key('name')), 'Nimali Perera');
      await tester.enterText(find.byKey(const Key('reg-email')), 'n@x.lk');
      await tester.enterText(find.byKey(const Key('reg-password')), 'abcdefgh');
      await tapText(tester, 'Create account');
      expect(find.text('Add at least one number.'), findsOneWidget);
      expect(app.server.calls, isEmpty);
    });

    testWidgets('registering creates the account and lands on the requests screen', (tester) async {
      final app = await TestApp.create({
        'POST /api/auth/register': (_) => authJson(),
        'GET /api/relief-requests': (_) => pagedJson([]),
      }, signedIn: false);
      await pump(tester, app);
      await tapText(tester, 'Create an account');
      await tester.enterText(find.byKey(const Key('name')), 'Nimali Perera');
      await tester.enterText(find.byKey(const Key('reg-email')), 'n@x.lk');
      await tester.enterText(find.byKey(const Key('reg-password')), 'Passw0rd1');
      await tapText(tester, 'Create account');
      expect(find.text('You have not asked for help yet.'), findsOneWidget);
    });
  });

  group('My requests', () {
    testWidgets('empty state invites the person to ask for help', (tester) async {
      await pump(tester, await TestApp.create({'GET /api/relief-requests': (_) => pagedJson([])}));
      expect(find.text('You have not asked for help yet.'), findsOneWidget);
    });

    testWidgets('shows each request with status, needs and how long ago', (tester) async {
      await pump(tester, await TestApp.create({
        'GET /api/relief-requests': (_) => pagedJson([requestJson(), requestJson(id: 'r2', status: 'InProgress', district: 'Galle')]),
      }));
      expect(find.text('Gampaha'), findsOneWidget);
      expect(find.text('Received'), findsOneWidget);
      expect(find.text('Help on the way'), findsOneWidget);
      expect(find.text('Water 20'), findsNWidgets(2));
      expect(find.text('Sent 3 h ago'), findsNWidgets(2));
    });

    testWidgets('a failed load shows the problem and a working Try again', (tester) async {
      var fail = true;
      await pump(tester, await TestApp.create({
        'GET /api/relief-requests': (_) => fail ? problem(500, 'Database is down') : pagedJson([requestJson()]),
      }));
      expect(find.text('Database is down'), findsOneWidget);
      fail = false;
      await tapText(tester, 'Try again');
      expect(find.text('Gampaha'), findsOneWidget);
      expect(find.text('Database is down'), findsNothing);
    });
  });

  group('Request detail', () {
    Future<void> open(WidgetTester tester, Map<String, dynamic> json, [Map<String, Object? Function(Recorded)> extra = const {}]) async {
      final app = await TestApp.create({'GET /api/relief-requests': (_) => pagedJson([json]), ...extra});
      await pump(tester, app);
      await tapText(tester, json['district'] as String);
    }

    testWidgets('a new request shows what happens next, progress, and both buttons', (tester) async {
      await open(tester, requestJson());
      expect(find.textContaining('A coordinator will look at it soon'), findsOneWidget);
      expect(find.text('Progress'), findsOneWidget);
      expect(find.text('Change my request'), findsOneWidget);
      expect(find.text('Cancel this request'), findsOneWidget);
    });

    testWidgets('once under review it can be cancelled but not changed', (tester) async {
      await open(tester, requestJson(status: 'UnderReview'));
      expect(find.text('Change my request'), findsNothing);
      expect(find.text('Cancel this request'), findsOneWidget);
    });

    for (final status in ['Approved', 'InProgress', 'Fulfilled', 'Rejected', 'Cancelled']) {
      testWidgets('a $status request has no change or cancel button', (tester) async {
        await open(tester, requestJson(status: status));
        expect(find.text('Change my request'), findsNothing);
        expect(find.text('Cancel this request'), findsNothing);
      });
    }

    testWidgets('a cancelled request shows the reason and no progress steps', (tester) async {
      await open(tester, requestJson(status: 'Cancelled', cancellationReason: 'Duplicate request'));
      expect(find.text('Reason given: Duplicate request'), findsOneWidget);
      expect(find.text('Progress'), findsNothing);
    });

    testWidgets('an approved request can find nearby shelters', (tester) async {
      final app = await TestApp.create({
        'GET /api/relief-requests': (_) => pagedJson([requestJson(status: 'Approved')]),
        'GET /api/shelters/nearest': (_) => [
          {'id': 's1', 'name': 'Kalutara Hall', 'district': 'Gampaha', 'address': 'Main Road', 'latitude': 6.58, 'longitude': 79.96, 'capacity': 100, 'occupied': 40, 'available': 60, 'isActive': true},
        ],
      });
      await pump(tester, app);
      await tapText(tester, 'Gampaha');
      await tapText(tester, 'Find nearby shelters');
      expect(find.text('Nearby shelters'), findsOneWidget);
      expect(find.text('Kalutara Hall'), findsOneWidget);
      expect(find.text('60 spaces available'), findsOneWidget);
    });

    testWidgets('cancelling asks first, then sends the reason', (tester) async {
      final app = await TestApp.create({
        'GET /api/relief-requests': (_) => pagedJson([requestJson()]),
        'POST /api/relief-requests/r1/cancel': (_) => requestJson(status: 'Cancelled', cancellationReason: 'Help arrived from a neighbour'),
      });
      await pump(tester, app);
      await tapText(tester, 'Gampaha');
      await tapText(tester, 'Cancel this request');
      expect(find.text('Cancel this request?'), findsOneWidget);

      // "Keep request" changes nothing.
      await tapText(tester, 'Keep request');
      expect(app.server.calls.where((c) => c.method == 'POST'), isEmpty);

      await tapText(tester, 'Cancel this request');
      await tester.enterText(find.byKey(const Key('cancel-reason')), 'Help arrived from a neighbour');
      await tapText(tester, 'Cancel request');
      expect(find.text('You cancelled this request.'), findsOneWidget);
      expect((app.server.calls.last.body as Map)['reason'], 'Help arrived from a neighbour');
    });

    testWidgets('if cancelling is refused, the message shows and the real state is reloaded', (tester) async {
      final app = await TestApp.create({
        'GET /api/relief-requests': (_) => pagedJson([requestJson()]),
        'POST /api/relief-requests/r1/cancel': (_) => problem(409, 'A request that is Approved cannot be cancelled.'),
        'GET /api/relief-requests/r1': (_) => requestJson(status: 'Approved'),
      });
      await pump(tester, app);
      await tapText(tester, 'Gampaha');
      await tapText(tester, 'Cancel this request');
      await tapText(tester, 'Cancel request');
      expect(find.text('A request that is Approved cannot be cancelled.'), findsOneWidget);
      expect(find.text('Cancel this request'), findsNothing); // refreshed: now Approved
    });
  });

  group('Ask for help form', () {
    Future<TestApp> openForm(WidgetTester tester, {Map<String, Object? Function(Recorded)> extra = const {}, FakeLocation? location}) async {
      final app = await TestApp.create({'GET /api/relief-requests': (_) => pagedJson([]), ...extra}, location: location);
      await pump(tester, app);
      await tapText(tester, 'Ask for help');
      return app;
    }

    Future<void> chooseDistrict(WidgetTester tester, String name) async {
      await tester.ensureVisible(find.byKey(const Key('district')));
      await tester.tap(find.byKey(const Key('district')));
      await tester.pumpAndSettle();
      await tester.tap(find.text(name).last);
      await tester.pumpAndSettle();
    }

    testWidgets('shows the emergency numbers first', (tester) async {
      await openForm(tester);
      expect(find.textContaining('call 117'), findsOneWidget);
    });

    testWidgets('an empty form shows what is missing and sends nothing', (tester) async {
      final app = await openForm(tester);
      await tapText(tester, 'Send request');
      expect(find.text('Tick at least one thing you need.'), findsWidgets); // in the summary and next to the field
      expect(find.text('Please fix these before sending:'), findsOneWidget);
      expect(find.text('Choose your district.'), findsWidgets); // in the summary and next to the field
      expect(find.text('Write your address or a nearby landmark (5 to 300 characters).'), findsWidgets); // in the summary and next to the field
      expect(app.server.calls.where((c) => c.method == 'POST'), isEmpty);
    });

    testWidgets('a complete form sends the right JSON and returns to the list', (tester) async {
      final app = await openForm(tester, extra: {'POST /api/relief-requests': (_) => requestJson()});
      await tapText(tester, 'Water');
      await tester.ensureVisible(find.byTooltip('More: How many'));
      await tester.tap(find.byTooltip('More: How many'));
      await tester.pump();
      await tester.tap(find.byTooltip('More: People in the household'));
      await tester.pump();
      await chooseDistrict(tester, 'Galle');
      await tester.enterText(find.byKey(const Key('address')), '48 Fort Road, Galle');
      await tapText(tester, 'Send request');

      final post = app.server.calls.firstWhere((c) => c.method == 'POST');
      final body = post.body as Map;
      expect(body['householdSize'], 2);
      expect(body['district'], 'Galle');
      expect(body['addressText'], '48 Fort Road, Galle');
      expect(body['items'], [{'category': 'Water', 'quantity': 2}]);
      expect(body['latitude'], districtByName('Galle')!.latitude);
      expect(body['neededBy'], isNull); // "I am not sure" is the default
      expect(find.text('My requests'), findsOneWidget);
    });

    testWidgets('elderly plus children cannot exceed the household', (tester) async {
      await openForm(tester);
      for (var i = 0; i < 2; i++) {
        await tester.ensureVisible(find.byTooltip('More: Elderly people'));
        await tester.tap(find.byTooltip('More: Elderly people'));
        await tester.pump();
      }
      await tapText(tester, 'Send request');
      expect(find.text('Elderly people and children cannot be more than the people in the household.'), findsWidgets); // in the summary and next to the field
    });

    testWidgets('the share-location button puts the real position in the request', (tester) async {
      final app = await openForm(tester,
          extra: {'POST /api/relief-requests': (_) => requestJson()}, location: FakeLocation(point: (latitude: 6.2, longitude: 80.1)));
      await tapText(tester, 'Use my current location');
      expect(find.text('Your location will be sent with the request.'), findsOneWidget);
      await tapText(tester, 'Water');
      await chooseDistrict(tester, 'Galle');
      await tester.enterText(find.byKey(const Key('address')), '48 Fort Road, Galle');
      await tapText(tester, 'Send request');
      final body = app.server.calls.firstWhere((c) => c.method == 'POST').body as Map;
      expect(body['latitude'], 6.2);
      expect(body['longitude'], 80.1);
    });

    testWidgets('a phone position outside Sri Lanka is not kept, so the request can still be sent', (tester) async {
      final app = await openForm(tester,
          extra: {'POST /api/relief-requests': (_) => requestJson()}, location: FakeLocation(point: (latitude: 37.4, longitude: -122.1)));
      await tapText(tester, 'Use my current location');
      expect(find.textContaining('outside Sri Lanka, so we will use the centre'), findsOneWidget);
      await tapText(tester, 'Water');
      await chooseDistrict(tester, 'Colombo');
      await tester.enterText(find.byKey(const Key('address')), 'Panadura town');
      await tapText(tester, 'Send request');
      final body = app.server.calls.firstWhere((c) => c.method == 'POST').body as Map;
      expect(body['latitude'], isNot(37.4)); // the district centre was sent instead
      expect(body['latitude'], inInclusiveRange(minLatitude, maxLatitude));
    });

    testWidgets('if location is not available the person is told what to do instead', (tester) async {
      await openForm(tester, location: FakeLocation(failure: 'Location is switched off on this phone. Switch it on, or choose your district instead.'));
      await tapText(tester, 'Use my current location');
      expect(find.textContaining('choose your district instead'), findsOneWidget);
    });

    testWidgets('when the server rejects the request, everything typed stays on screen', (tester) async {
      final app = await openForm(tester, extra: {
        'POST /api/relief-requests': (_) => http.Response(
            '{"title":"One or more validation errors occurred.","errors":{"Location":["Coordinates must be inside Sri Lanka."]}}', 400),
      });
      await tapText(tester, 'Water');
      await chooseDistrict(tester, 'Galle');
      await tester.enterText(find.byKey(const Key('address')), '48 Fort Road, Galle');
      await tapText(tester, 'Send request');
      expect(find.text('One or more validation errors occurred.'), findsOneWidget);
      expect(find.textContaining('Coordinates must be inside Sri Lanka.'), findsOneWidget);
      expect(find.text('48 Fort Road, Galle'), findsOneWidget);
      expect(find.text('Send request'), findsOneWidget);
      expect(app.server.calls.where((c) => c.method == 'POST'), hasLength(1));
    });

    testWidgets('changing a Submitted request starts from its saved values and uses PUT', (tester) async {
      final app = await TestApp.create({
        'GET /api/relief-requests': (_) => pagedJson([requestJson()]),
        'PUT /api/relief-requests/r1': (_) => requestJson(),
      });
      await pump(tester, app);
      await tapText(tester, 'Gampaha');
      await tapText(tester, 'Change my request');
      expect(find.text('Change my request'), findsWidgets);
      expect(find.text('12 Temple Road, Kelaniya'), findsOneWidget);
      await tapText(tester, 'Save changes');
      final put = app.server.calls.firstWhere((c) => c.method == 'PUT');
      final body = put.body as Map;
      expect(body['district'], 'Gampaha');
      expect(body['latitude'], 6.95); // the saved position is kept
      expect((body['items'] as List).length, 2);
    });
  });
}
