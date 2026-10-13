import '../api/models.dart';
import '../data/districts.dart';

/// What the person has typed so far. Becomes the JSON the API expects.
class RequestDraft {
  RequestDraft({
    this.householdSize = 1,
    this.elderlyCount = 0,
    this.childrenCount = 0,
    this.homeFlooded = false,
    this.district,
    this.addressText = '',
    this.latitude,
    this.longitude,
    this.notes = '',
    this.neededBy,
    Map<String, int>? needs,
  }) : needs = needs ?? {};

  int householdSize;
  int elderlyCount;
  int childrenCount;
  bool homeFlooded;
  String? district;
  String addressText;
  double? latitude;
  double? longitude;
  String notes;
  DateTime? neededBy;

  /// Category -> quantity, only for the things the person ticked.
  Map<String, int> needs;

  factory RequestDraft.from(ReliefRequest r) => RequestDraft(
        householdSize: r.householdSize,
        elderlyCount: r.elderlyCount,
        childrenCount: r.childrenCount,
        homeFlooded: r.homeFlooded,
        district: r.district,
        addressText: r.addressText,
        latitude: r.latitude,
        longitude: r.longitude,
        notes: r.notes ?? '',
        neededBy: r.neededBy,
        needs: {for (final i in r.items) i.category: i.quantity},
      );

  /// Location to send: the shared one, otherwise the middle of the chosen district.
  ({double latitude, double longitude})? get effectiveLocation {
    if (latitude != null && longitude != null) return (latitude: latitude!, longitude: longitude!);
    final d = districtByName(district);
    return d == null ? null : (latitude: d.latitude, longitude: d.longitude);
  }

  Map<String, dynamic> toJson() {
    final where = effectiveLocation;
    return {
      'householdSize': householdSize,
      'elderlyCount': elderlyCount,
      'childrenCount': childrenCount,
      'homeFlooded': homeFlooded,
      'district': district,
      'addressText': addressText.trim(),
      'latitude': where?.latitude,
      'longitude': where?.longitude,
      'notes': notes.trim().isEmpty ? null : notes.trim(),
      'neededBy': neededBy?.toUtc().toIso8601String(),
      'items': [
        for (final c in needCategories)
          if (needs.containsKey(c)) {'category': c, 'quantity': needs[c]},
      ],
    };
  }
}

const maxHouseholdSize = 50;
const maxNeeds = 6;
const maxQuantity = 1000;

// A rectangle around Sri Lanka, the same one the API uses.
const minLatitude = 5.5, maxLatitude = 10.0, minLongitude = 79.4, maxLongitude = 82.0;

/// The same rules the API enforces, checked on the phone first so the person gets an instant,
/// specific message. The API still checks everything again: this is a convenience, not security.
/// Returns field name -> message. An empty map means the form is fine.
Map<String, String> validateDraft(RequestDraft d, DateTime now) {
  final errors = <String, String>{};

  if (d.householdSize < 1 || d.householdSize > maxHouseholdSize) {
    errors['householdSize'] = 'Enter a number of people between 1 and $maxHouseholdSize.';
  }
  if (d.elderlyCount < 0 || d.childrenCount < 0) {
    errors['people'] = 'Numbers of elderly people and children cannot be negative.';
  } else if (d.elderlyCount + d.childrenCount > d.householdSize) {
    errors['people'] = 'Elderly people and children cannot be more than the people in the household.';
  }

  if (districtByName(d.district) == null) errors['district'] = 'Choose your district.';

  final address = d.addressText.trim();
  if (address.length < 5 || address.length > 300) {
    errors['addressText'] = 'Write your address or a nearby landmark (5 to 300 characters).';
  }

  final where = d.effectiveLocation;
  if (where != null &&
      (where.latitude < minLatitude || where.latitude > maxLatitude || where.longitude < minLongitude || where.longitude > maxLongitude)) {
    errors['location'] = 'The location is outside Sri Lanka. Choose your district instead.';
  }

  if (d.notes.trim().length > 1000) errors['notes'] = 'Notes can be at most 1000 characters.';

  if (d.needs.isEmpty) {
    errors['needs'] = 'Tick at least one thing you need.';
  } else if (d.needs.length > maxNeeds) {
    errors['needs'] = 'Choose at most $maxNeeds things.';
  } else if (d.needs.values.any((q) => q < 1 || q > maxQuantity)) {
    errors['needs'] = 'Each amount must be between 1 and $maxQuantity.';
  }

  final by = d.neededBy;
  if (by != null) {
    if (by.isBefore(now.subtract(const Duration(minutes: 5)))) {
      errors['neededBy'] = 'The time needed cannot be in the past.';
    } else if (by.isAfter(now.add(const Duration(days: 30)))) {
      errors['neededBy'] = 'The time needed must be within 30 days.';
    }
  }

  return errors;
}
