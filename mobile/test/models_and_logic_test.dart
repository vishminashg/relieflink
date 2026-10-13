import 'package:flutter_test/flutter_test.dart';
import 'package:relieflink_mobile/api/models.dart';
import 'package:relieflink_mobile/data/districts.dart';
import 'package:relieflink_mobile/logic/request_form.dart';
import 'package:relieflink_mobile/logic/status_text.dart';

import 'support/fakes.dart';

RequestDraft goodDraft() => RequestDraft(
      householdSize: 4, elderlyCount: 1, childrenCount: 1, district: 'Galle', addressText: '48 Fort Road, Galle', needs: {'Water': 6});

void main() {
  group('districts', () {
    test('there are 25, all distinct, and every centre is inside the API\'s Sri Lanka box', () {
      expect(districts.length, 25);
      expect(districts.map((d) => d.name).toSet().length, 25);
      for (final d in districts) {
        expect(d.latitude, inInclusiveRange(minLatitude, maxLatitude), reason: d.name);
        expect(d.longitude, inInclusiveRange(minLongitude, maxLongitude), reason: d.name);
      }
    });
    test('lookup by name', () {
      expect(districtByName('Kandy')!.latitude, closeTo(7.29, 0.01));
      expect(districtByName('Atlantis'), isNull);
      expect(districtByName(null), isNull);
    });
  });

  group('ReliefRequest', () {
    test('reads the API JSON', () {
      final r = ReliefRequest.fromJson(requestJson(neededBy: '2026-10-14T06:00:00Z'));
      expect(r.district, 'Gampaha');
      expect(r.items.map((i) => i.category), ['Water', 'Food']);
      expect(r.neededBy, DateTime.utc(2026, 10, 14, 6));
    });
    test('canEdit only while Submitted', () {
      expect(ReliefRequest.fromJson(requestJson(status: 'Submitted')).canEdit, isTrue);
      for (final s in ['UnderReview', 'PlanReady', 'Approved', 'InProgress', 'Fulfilled', 'Rejected', 'Cancelled']) {
        expect(ReliefRequest.fromJson(requestJson(status: s)).canEdit, isFalse, reason: s);
      }
    });
    test('canCancel matches the API rule', () {
      const yes = ['Submitted', 'UnderReview', 'PlanReady'];
      for (final s in ['Submitted', 'UnderReview', 'PlanReady', 'Approved', 'InProgress', 'Fulfilled', 'Rejected', 'Cancelled']) {
        expect(ReliefRequest.fromJson(requestJson(status: s)).canCancel, yes.contains(s), reason: s);
      }
    });
  });

  group('status wording', () {
    test('every status has a label and a next-step sentence', () {
      for (final s in [...statusSteps, 'Rejected', 'Cancelled']) {
        expect(statusLabel(s), isNot(s == 'Approved' ? 'x' : ''), reason: s);
        expect(statusNextStep(s), isNotEmpty, reason: s);
      }
    });
    test('closed statuses', () {
      expect(isClosedStatus('Fulfilled'), isTrue);
      expect(isClosedStatus('Cancelled'), isTrue);
      expect(isClosedStatus('InProgress'), isFalse);
    });
    test('timeAgo', () {
      final now = DateTime.utc(2026, 10, 13, 12);
      expect(timeAgo(now, now), 'just now');
      expect(timeAgo(now.subtract(const Duration(minutes: 5)), now), '5 min ago');
      expect(timeAgo(now.subtract(const Duration(hours: 3)), now), '3 h ago');
      expect(timeAgo(now.subtract(const Duration(days: 1, hours: 2)), now), '1 day ago');
      expect(timeAgo(now.subtract(const Duration(days: 4)), now), '4 days ago');
    });
  });

  group('validateDraft', () {
    test('a good draft has no errors', () => expect(validateDraft(goodDraft(), fixedNow), isEmpty));

    test('household size must be 1 to 50', () {
      expect(validateDraft(goodDraft()..householdSize = 0, fixedNow), contains('householdSize'));
      expect(validateDraft(goodDraft()..householdSize = 51, fixedNow), contains('householdSize'));
      expect(validateDraft(goodDraft()..householdSize = 50, fixedNow).containsKey('householdSize'), isFalse);
    });
    test('elderly plus children cannot exceed the household', () {
      expect(validateDraft(goodDraft()..elderlyCount = 3..childrenCount = 2, fixedNow), contains('people'));
      expect(validateDraft(goodDraft()..elderlyCount = 2..childrenCount = 2, fixedNow).containsKey('people'), isFalse);
    });
    test('district must be one of the 25', () {
      expect(validateDraft(goodDraft()..district = null, fixedNow), contains('district'));
      expect(validateDraft(goodDraft()..district = 'Atlantis', fixedNow), contains('district'));
    });
    test('address 5 to 300 characters after trimming', () {
      expect(validateDraft(goodDraft()..addressText = '  abcd  ', fixedNow), contains('addressText'));
      expect(validateDraft(goodDraft()..addressText = 'x' * 301, fixedNow), contains('addressText'));
      expect(validateDraft(goodDraft()..addressText = 'abcde', fixedNow).containsKey('addressText'), isFalse);
    });
    test('needs: at least one, quantities 1 to 1000', () {
      expect(validateDraft(goodDraft()..needs = {}, fixedNow), contains('needs'));
      expect(validateDraft(goodDraft()..needs = {'Water': 0}, fixedNow), contains('needs'));
      expect(validateDraft(goodDraft()..needs = {'Water': 1001}, fixedNow), contains('needs'));
      expect(validateDraft(goodDraft()..needs = {'Water': 1000}, fixedNow), isEmpty);
    });
    test('a shared location outside Sri Lanka is refused', () {
      expect(validateDraft(goodDraft()..latitude = 51.5..longitude = -0.12, fixedNow), contains('location'));
      expect(validateDraft(goodDraft()..latitude = 6.0..longitude = 80.2, fixedNow), isEmpty);
    });
    test('deadline cannot be in the past or beyond 30 days', () {
      expect(validateDraft(goodDraft()..neededBy = fixedNow.subtract(const Duration(hours: 1)), fixedNow), contains('neededBy'));
      expect(validateDraft(goodDraft()..neededBy = fixedNow.add(const Duration(days: 31)), fixedNow), contains('neededBy'));
      expect(validateDraft(goodDraft()..neededBy = fixedNow.add(const Duration(hours: 6)), fixedNow), isEmpty);
    });
    test('notes at most 1000 characters', () {
      expect(validateDraft(goodDraft()..notes = 'n' * 1001, fixedNow), contains('notes'));
    });
  });

  group('RequestDraft.toJson', () {
    test('uses the shared location when there is one', () {
      final json = (goodDraft()..latitude = 6.1..longitude = 80.3).toJson();
      expect(json['latitude'], 6.1);
      expect(json['longitude'], 80.3);
    });
    test('falls back to the centre of the district', () {
      final json = goodDraft().toJson();
      expect(json['latitude'], districtByName('Galle')!.latitude);
      expect(json['longitude'], districtByName('Galle')!.longitude);
    });
    test('sends needs in a fixed order, trims text, and never sends status or score', () {
      final json = (goodDraft()..needs = {'Water': 6, 'Food': 2}..addressText = '  48 Fort Road  '..notes = '  ').toJson();
      expect((json['items'] as List).map((i) => (i as Map)['category']), ['Food', 'Water']);
      expect(json['addressText'], '48 Fort Road');
      expect(json['notes'], isNull);
      expect(json.keys, isNot(containsAll(['status', 'triageScore', 'urgency'])));
      expect(json.containsKey('status'), isFalse);
      expect(json.containsKey('triageScore'), isFalse);
      expect(json.containsKey('urgency'), isFalse);
    });
    test('deadline is sent in UTC', () {
      final json = (goodDraft()..neededBy = DateTime.utc(2026, 10, 14, 6)).toJson();
      expect(json['neededBy'], '2026-10-14T06:00:00.000Z');
    });
    test('editing starts from the saved request', () {
      final draft = RequestDraft.from(ReliefRequest.fromJson(requestJson()));
      expect(draft.needs, {'Water': 20, 'Food': 5});
      expect(draft.district, 'Gampaha');
      expect(draft.latitude, 6.95);
    });
  });
}
