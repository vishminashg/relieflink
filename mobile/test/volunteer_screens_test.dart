import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fakes.dart';

/// Pump the widget tree with a fixed viewport to avoid IndexedStack semantics issues.
Future<void> pump(WidgetTester tester, TestApp app) async {
  tester.view.physicalSize = const Size(900, 1800);
  tester.view.devicePixelRatio = 2;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(app.widget);
  await tester.pumpAndSettle();
}

void main() {
  group('Volunteer Mobile Screens', () {
    testWidgets('loads VolunteerHomeScreen with 3 tabs when user is Volunteer', (tester) async {
      final app = await TestApp.create(
        {
          'GET /api/assignments/my': (_) => [volunteerAssignmentJson()],
          'GET /api/volunteers/me': (_) => volunteerProfileJson(),
        },
        role: 'Volunteer',
      );

      await pump(tester, app);

      // Should show Volunteer UI with Bottom Navigation Bar
      expect(find.text('Missions'), findsOneWidget);
      expect(find.text('Availability'), findsOneWidget);
      expect(find.text('Profile'), findsOneWidget);

      // Default tab is Field Missions
      expect(find.text('Field Missions'), findsOneWidget);
      expect(find.text('First Aid Response'), findsOneWidget);
      expect(find.text('Assigned'), findsOneWidget);
    });

    testWidgets('starts mission and updates status to InProgress', (tester) async {
      var statusUpdated = false;
      // After PATCH the reload GET should return updated status so pumpAndSettle terminates.
      var getCallCount = 0;

      final app = await TestApp.create(
        {
          'GET /api/assignments/my': (_) {
            getCallCount++;
            // First load: Assigned; subsequent reloads: InProgress (post-action).
            return [volunteerAssignmentJson(status: getCallCount == 1 ? 'Assigned' : 'InProgress')];
          },
          'GET /api/volunteers/me': (_) => volunteerProfileJson(),
          'PATCH /api/assignments/asg-1/status': (rec) {
            statusUpdated = true;
            return volunteerAssignmentJson(status: 'InProgress');
          },
        },
        role: 'Volunteer',
      );

      await pump(tester, app);

      final startBtn = find.text('Start Mission');
      expect(startBtn, findsOneWidget);

      await tester.tap(startBtn);
      await tester.pumpAndSettle();

      expect(statusUpdated, isTrue);
    });

    testWidgets('completes mission with debrief notes', (tester) async {
      String? sentStatus;
      String? sentNotes;
      var getCallCount = 0;

      final app = await TestApp.create(
        {
          'GET /api/assignments/my': (_) {
            getCallCount++;
            return [volunteerAssignmentJson(status: getCallCount == 1 ? 'InProgress' : 'Completed')];
          },
          'GET /api/volunteers/me': (_) => volunteerProfileJson(),
          'PATCH /api/assignments/asg-1/status': (rec) {
            final body = rec.body as Map<String, dynamic>;
            sentStatus = body['status'] as String?;
            sentNotes = body['notes'] as String?;
            return volunteerAssignmentJson(status: 'Completed');
          },
        },
        role: 'Volunteer',
      );

      await pump(tester, app);

      final completeBtn = find.text('Complete Mission');
      expect(completeBtn, findsOneWidget);

      await tester.tap(completeBtn);
      await tester.pumpAndSettle();

      // Dialog opens
      expect(find.text('Complete Field Mission'), findsOneWidget);
      await tester.enterText(find.byType(TextField).last, 'All families provided first aid kits.');
      await tester.tap(find.text('Confirm Completed'));
      await tester.pumpAndSettle();

      expect(sentStatus, equals('Completed'));
      expect(sentNotes, equals('All families provided first aid kits.'));
    });

    testWidgets('withdraws/cancels assignment with reason', (tester) async {
      String? cancelReason;
      var getCallCount = 0;

      final app = await TestApp.create(
        {
          'GET /api/assignments/my': (_) {
            getCallCount++;
            return [volunteerAssignmentJson(status: getCallCount == 1 ? 'Assigned' : 'Cancelled')];
          },
          'GET /api/volunteers/me': (_) => volunteerProfileJson(),
          'POST /api/assignments/asg-1/cancel': (rec) {
            final body = rec.body as Map<String, dynamic>;
            cancelReason = body['reason'] as String?;
            return volunteerAssignmentJson(status: 'Cancelled');
          },
        },
        role: 'Volunteer',
      );

      await pump(tester, app);

      final withdrawBtn = find.text('Withdraw');
      expect(withdrawBtn, findsOneWidget);

      await tester.tap(withdrawBtn);
      await tester.pumpAndSettle();

      expect(find.text('Unable to Attend Mission'), findsOneWidget);
      await tester.enterText(find.byType(TextField).last, 'Road flooded, vehicle stuck');
      await tester.tap(find.text('Withdraw from Mission'));
      await tester.pumpAndSettle();

      expect(cancelReason, equals('Road flooded, vehicle stuck'));
    });

    testWidgets('switches to Availability tab and views slots', (tester) async {
      final app = await TestApp.create(
        {
          'GET /api/assignments/my': (_) => [],
          'GET /api/volunteers/me': (_) => volunteerProfileJson(),
        },
        role: 'Volunteer',
      );

      await pump(tester, app);

      // Tap Availability tab
      await tester.tap(find.text('Availability'));
      await tester.pumpAndSettle();

      expect(find.text('My Duty Availability'), findsOneWidget);
      expect(find.text('Every Monday'), findsOneWidget);
      expect(find.text('Add Availability'), findsOneWidget);
    });

    testWidgets('switches to Profile tab and views certified skills', (tester) async {
      final app = await TestApp.create(
        {
          'GET /api/assignments/my': (_) => [],
          'GET /api/volunteers/me': (_) => volunteerProfileJson(),
        },
        role: 'Volunteer',
      );

      await pump(tester, app);

      // Tap Profile tab
      await tester.tap(find.text('Profile'));
      await tester.pumpAndSettle();

      // 'Volunteer Profile' appears in both the AppBar and as a SectionCard title
      expect(find.text('Volunteer Profile'), findsAtLeast(1));
      expect(find.text('Colombo'), findsOneWidget);
      expect(find.text('Kamala Fernando (0779988776)'), findsOneWidget);
      expect(find.text('FirstAid'), findsOneWidget);
      expect(find.text('Verified ✓'), findsOneWidget);
      expect(find.text('Add Certified Skill'), findsOneWidget);
    });
  });
}
