/// The 25 districts the API accepts, with a point near each district's main town.
/// The point is only used when the person does not share their location.
class District {
  const District(this.name, this.latitude, this.longitude);
  final String name;
  final double latitude;
  final double longitude;
}

const List<District> districts = [
  District('Ampara', 7.30, 81.67),
  District('Anuradhapura', 8.31, 80.41),
  District('Badulla', 6.99, 81.06),
  District('Batticaloa', 7.71, 81.70),
  District('Colombo', 6.93, 79.86),
  District('Galle', 6.05, 80.22),
  District('Gampaha', 7.09, 79.99),
  District('Hambantota', 6.12, 81.12),
  District('Jaffna', 9.66, 80.01),
  District('Kalutara', 6.58, 79.96),
  District('Kandy', 7.29, 80.63),
  District('Kegalle', 7.25, 80.35),
  District('Kilinochchi', 9.39, 80.40),
  District('Kurunegala', 7.49, 80.37),
  District('Mannar', 8.98, 79.90),
  District('Matale', 7.47, 80.62),
  District('Matara', 5.95, 80.55),
  District('Monaragala', 6.87, 81.35),
  District('Mullaitivu', 9.27, 80.81),
  District('Nuwara Eliya', 6.97, 80.77),
  District('Polonnaruwa', 7.94, 81.00),
  District('Puttalam', 8.03, 79.83),
  District('Ratnapura', 6.68, 80.40),
  District('Trincomalee', 8.57, 81.23),
  District('Vavuniya', 8.75, 80.50),
];

District? districtByName(String? name) {
  for (final d in districts) {
    if (d.name == name) return d;
  }
  return null;
}
