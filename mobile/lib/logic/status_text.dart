// Plain-language wording for each request status. Written for a worried family, not for a coordinator.

const statusSteps = ['Submitted', 'UnderReview', 'PlanReady', 'Approved', 'InProgress', 'Fulfilled'];

const _label = {
  'Submitted': 'Received',
  'UnderReview': 'Being reviewed',
  'PlanReady': 'Plan ready',
  'Approved': 'Approved',
  'InProgress': 'Help on the way',
  'Fulfilled': 'Completed',
  'Rejected': 'Not accepted',
  'Cancelled': 'Cancelled',
};

const _next = {
  'Submitted': 'We have your request. A coordinator will look at it soon.',
  'UnderReview': 'A coordinator is preparing a plan for your household.',
  'PlanReady': 'A plan is ready. It is waiting for a coordinator to approve it.',
  'Approved': 'Your plan is approved. Help is being arranged.',
  'InProgress': 'Help is on its way to you.',
  'Fulfilled': 'This request is complete.',
  'Rejected': 'This request was not accepted. If you still need help, send a new request or contact your district office.',
  'Cancelled': 'You cancelled this request.',
};

String statusLabel(String status) => _label[status] ?? status;

String statusNextStep(String status) => _next[status] ?? '';

bool isClosedStatus(String status) => status == 'Fulfilled' || status == 'Rejected' || status == 'Cancelled';

/// "2 h ago", "3 days ago": how long ago something happened.
String timeAgo(DateTime then, DateTime now) {
  final minutes = now.difference(then).inMinutes;
  if (minutes < 1) return 'just now';
  if (minutes < 60) return '$minutes min ago';
  final hours = minutes ~/ 60;
  if (hours < 24) return '$hours h ago';
  final days = hours ~/ 24;
  return days == 1 ? '1 day ago' : '$days days ago';
}
