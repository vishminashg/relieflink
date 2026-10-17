// Plain data classes that mirror the JSON the ReliefLink API sends.

class AppUser {
  const AppUser({required this.id, required this.fullName, required this.email, required this.role, this.phone, this.district});

  final String id;
  final String fullName;
  final String email;
  final String role;
  final String? phone;
  final String? district;

  factory AppUser.fromJson(Map<String, dynamic> json) => AppUser(
        id: json['id'] as String,
        fullName: json['fullName'] as String,
        email: json['email'] as String,
        role: json['role'] as String,
        phone: json['phone'] as String?,
        district: json['district'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'id': id, 'fullName': fullName, 'email': email, 'role': role, 'phone': phone, 'district': district,
      };
}

class AuthResult {
  const AuthResult({required this.token, required this.expiresAt, required this.user});

  final String token;
  final DateTime expiresAt;
  final AppUser user;

  factory AuthResult.fromJson(Map<String, dynamic> json) => AuthResult(
        token: json['token'] as String,
        expiresAt: DateTime.parse(json['expiresAtUtc'] as String).toUtc(),
        user: AppUser.fromJson(json['user'] as Map<String, dynamic>),
      );
}

const needCategories = ['Food', 'Water', 'Shelter', 'Medical', 'Clothing', 'Sanitation'];

class NeedItem {
  const NeedItem({required this.category, required this.quantity, this.description});

  final String category;
  final int quantity;
  final String? description;

  factory NeedItem.fromJson(Map<String, dynamic> json) => NeedItem(
        category: json['category'] as String,
        quantity: (json['quantity'] as num).toInt(),
        description: json['description'] as String?,
      );

  Map<String, dynamic> toJson() => {'category': category, 'quantity': quantity, 'description': description};
}

class ReliefRequest {
  const ReliefRequest({
    required this.id,
    required this.householdSize,
    required this.elderlyCount,
    required this.childrenCount,
    required this.homeFlooded,
    required this.district,
    required this.addressText,
    required this.latitude,
    required this.longitude,
    required this.status,
    required this.urgency,
    required this.items,
    required this.createdAt,
    this.notes,
    this.neededBy,
    this.cancellationReason,
  });

  final String id;
  final int householdSize;
  final int elderlyCount;
  final int childrenCount;
  final bool homeFlooded;
  final String district;
  final String addressText;
  final double latitude;
  final double longitude;
  final String status;
  final String urgency;
  final List<NeedItem> items;
  final DateTime createdAt;
  final String? notes;
  final DateTime? neededBy;
  final String? cancellationReason;

  factory ReliefRequest.fromJson(Map<String, dynamic> json) => ReliefRequest(
        id: json['id'] as String,
        householdSize: (json['householdSize'] as num).toInt(),
        elderlyCount: (json['elderlyCount'] as num).toInt(),
        childrenCount: (json['childrenCount'] as num).toInt(),
        homeFlooded: json['homeFlooded'] as bool,
        district: json['district'] as String,
        addressText: json['addressText'] as String,
        latitude: (json['latitude'] as num).toDouble(),
        longitude: (json['longitude'] as num).toDouble(),
        status: json['status'] as String,
        urgency: json['urgency'] as String,
        items: (json['items'] as List<dynamic>).map((e) => NeedItem.fromJson(e as Map<String, dynamic>)).toList(),
        createdAt: DateTime.parse(json['createdAt'] as String).toUtc(),
        notes: json['notes'] as String?,
        neededBy: json['neededBy'] == null ? null : DateTime.parse(json['neededBy'] as String).toUtc(),
        cancellationReason: json['cancellationReason'] as String?,
      );

  /// A citizen may change a request only while nobody has started on it.
  bool get canEdit => status == 'Submitted';

  /// Same rule as the API: it can be cancelled while it still waits for a decision.
  bool get canCancel => status == 'Submitted' || status == 'UnderReview' || status == 'PlanReady';
}

class PagedRequests {
  const PagedRequests({required this.items, required this.page, required this.totalPages, required this.totalCount});

  final List<ReliefRequest> items;
  final int page;
  final int totalPages;
  final int totalCount;

  factory PagedRequests.fromJson(Map<String, dynamic> json) => PagedRequests(
        items: (json['items'] as List<dynamic>).map((e) => ReliefRequest.fromJson(e as Map<String, dynamic>)).toList(),
        page: (json['page'] as num).toInt(),
        totalPages: (json['totalPages'] as num).toInt(),
        totalCount: (json['totalCount'] as num).toInt(),
      );
}

// ==========================================
// Component C: Volunteer Management Models
// ==========================================

class VolunteerSkillModel {
  const VolunteerSkillModel({
    required this.id,
    required this.skill,
    required this.proficiency,
    this.certificateReference,
    required this.isVerified,
  });

  final String id;
  final String skill;
  final String proficiency;
  final String? certificateReference;
  final bool isVerified;

  factory VolunteerSkillModel.fromJson(Map<String, dynamic> json) => VolunteerSkillModel(
        id: json['id'] as String,
        skill: json['skill'] as String,
        proficiency: json['proficiency'] as String,
        certificateReference: json['certificateReference'] as String?,
        isVerified: json['isVerified'] as bool? ?? false,
      );

  Map<String, dynamic> toJson() => {
        'skill': skill,
        'proficiency': proficiency,
        if (certificateReference != null) 'certificateReference': certificateReference,
      };
}

class VolunteerAvailabilityModel {
  const VolunteerAvailabilityModel({
    required this.id,
    required this.type,
    this.dayOfWeek,
    this.startTime,
    this.endTime,
    this.specificStartUtc,
    this.specificEndUtc,
  });

  final String id;
  final String type;
  final String? dayOfWeek;
  final String? startTime;
  final String? endTime;
  final DateTime? specificStartUtc;
  final DateTime? specificEndUtc;

  factory VolunteerAvailabilityModel.fromJson(Map<String, dynamic> json) => VolunteerAvailabilityModel(
        id: json['id'] as String,
        type: json['type'] as String,
        dayOfWeek: json['dayOfWeek'] as String?,
        startTime: json['startTime'] as String?,
        endTime: json['endTime'] as String?,
        specificStartUtc: json['specificStartUtc'] == null ? null : DateTime.parse(json['specificStartUtc'] as String).toUtc(),
        specificEndUtc: json['specificEndUtc'] == null ? null : DateTime.parse(json['specificEndUtc'] as String).toUtc(),
      );

  Map<String, dynamic> toJson() => {
        'type': type,
        if (dayOfWeek != null) 'dayOfWeek': dayOfWeek,
        if (startTime != null) 'startTime': startTime,
        if (endTime != null) 'endTime': endTime,
        if (specificStartUtc != null) 'specificStartUtc': specificStartUtc!.toIso8601String(),
        if (specificEndUtc != null) 'specificEndUtc': specificEndUtc!.toIso8601String(),
      };
}

class VolunteerProfile {
  const VolunteerProfile({
    required this.id,
    required this.district,
    required this.emergencyContactName,
    required this.emergencyContactPhone,
    required this.isActive,
    required this.maxHoursPerWeek,
    this.notes,
    required this.skills,
    required this.availabilities,
    required this.activeAssignmentsCount,
  });

  final String id;
  final String district;
  final String emergencyContactName;
  final String emergencyContactPhone;
  final bool isActive;
  final int maxHoursPerWeek;
  final String? notes;
  final List<VolunteerSkillModel> skills;
  final List<VolunteerAvailabilityModel> availabilities;
  final int activeAssignmentsCount;

  factory VolunteerProfile.fromJson(Map<String, dynamic> json) => VolunteerProfile(
        id: json['id'] as String,
        district: json['district'] as String,
        emergencyContactName: json['emergencyContactName'] as String,
        emergencyContactPhone: json['emergencyContactPhone'] as String,
        isActive: json['isActive'] as bool,
        maxHoursPerWeek: (json['maxHoursPerWeek'] as num).toInt(),
        notes: json['notes'] as String?,
        skills: (json['skills'] as List<dynamic>?)
                ?.map((e) => VolunteerSkillModel.fromJson(e as Map<String, dynamic>))
                .toList() ??
            [],
        availabilities: (json['availabilities'] as List<dynamic>?)
                ?.map((e) => VolunteerAvailabilityModel.fromJson(e as Map<String, dynamic>))
                .toList() ??
            [],
        activeAssignmentsCount: (json['activeAssignmentsCount'] as num?)?.toInt() ?? 0,
      );
}

class VolunteerAssignmentModel {
  const VolunteerAssignmentModel({
    required this.id,
    required this.volunteerId,
    required this.volunteerName,
    this.reliefRequestId,
    required this.startTime,
    required this.endTime,
    required this.status,
    this.roleOrTask,
    this.notes,
  });

  final String id;
  final String volunteerId;
  final String volunteerName;
  final String? reliefRequestId;
  final DateTime startTime;
  final DateTime endTime;
  final String status;
  final String? roleOrTask;
  final String? notes;

  factory VolunteerAssignmentModel.fromJson(Map<String, dynamic> json) => VolunteerAssignmentModel(
        id: json['id'] as String,
        volunteerId: json['volunteerId'] as String,
        volunteerName: json['volunteerName'] as String,
        reliefRequestId: json['reliefRequestId'] as String?,
        startTime: DateTime.parse(json['startTime'] as String).toUtc(),
        endTime: DateTime.parse(json['endTime'] as String).toUtc(),
        status: json['status'] as String,
        roleOrTask: json['roleOrTask'] as String?,
        notes: json['notes'] as String?,
      );

  bool get canStart => status == 'Assigned';
  bool get canComplete => status == 'InProgress';
  bool get canCancel => status == 'Assigned' || status == 'InProgress';
}

