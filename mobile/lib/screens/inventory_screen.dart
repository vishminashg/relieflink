import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../theme.dart';
import 'home_screen.dart';

class InventoryScreen extends StatefulWidget {
  const InventoryScreen({super.key});

  @override
  State<InventoryScreen> createState() => _InventoryScreenState();
}

class _InventoryScreenState extends State<InventoryScreen> {
  List<StockItemInfo>? _items;
  ApiException? _error;
  bool _loading = true;
  String _selectedCategory = '';

  final _categories = ['All', 'Food', 'Water', 'Medical', 'Shelter', 'Sanitation', 'Clothing'];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final categoryFilter = _selectedCategory == 'All' || _selectedCategory.isEmpty ? null : _selectedCategory;
      final list = await context.read<ApiClient>().getStockLevels(category: categoryFilter);
      if (mounted) setState(() => _items = list);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Relief Supplies & Depots'),
        actions: const [SignOutButton()],
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: Column(
          children: [
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Row(
                children: _categories.map((c) {
                  final isSelected = (_selectedCategory == c) || (_selectedCategory.isEmpty && c == 'All');
                  return Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text(c),
                      selected: isSelected,
                      onSelected: (selected) {
                        if (selected) {
                          setState(() => _selectedCategory = c == 'All' ? '' : c);
                          _load();
                        }
                      },
                    ),
                  );
                }).toList(),
              ),
            ),
            const Divider(height: 1),
            Expanded(
              child: _buildBody(),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildBody() {
    if (_loading && (_items == null || _items!.isEmpty)) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, size: 48, color: critical),
              const SizedBox(height: 12),
              Text(_error!.message, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              ElevatedButton(onPressed: _load, child: const Text('Retry')),
            ],
          ),
        ),
      );
    }
    if (_items == null || _items!.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.inventory_2_outlined, size: 48, color: Colors.grey),
              SizedBox(height: 12),
              Text('No stock available in the selected category.', style: TextStyle(color: Colors.grey, fontSize: 16)),
            ],
          ),
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _items!.length,
      separatorBuilder: (_, __) => const SizedBox(height: 10),
      itemBuilder: (_, i) {
        final item = _items![i];
        return Card(
          elevation: 1,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(8),
            side: BorderSide(color: item.isLowStock ? critical.withValues(alpha: 0.5) : Colors.grey.shade300),
          ),
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                            decoration: BoxDecoration(
                              color: signal.withValues(alpha: 0.1),
                              borderRadius: BorderRadius.circular(4),
                            ),
                            child: Text(
                              item.category,
                              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.bold, color: signal),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Text(item.itemCode, style: const TextStyle(fontSize: 12, color: Colors.grey)),
                        ],
                      ),
                      const SizedBox(height: 6),
                      Text(item.itemName, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                      const SizedBox(height: 4),
                      Row(
                        children: [
                          const Icon(Icons.warehouse, size: 16, color: Colors.grey),
                          const SizedBox(width: 4),
                          Text('${item.depotName} (${item.depotDistrict})', style: const TextStyle(fontSize: 13, color: Colors.grey)),
                        ],
                      ),
                    ],
                  ),
                ),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(
                      '${item.quantityAvailable}',
                      style: TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                        color: item.isLowStock ? critical : signal,
                      ),
                    ),
                    Text(item.unitOfMeasure, style: const TextStyle(fontSize: 12, color: Colors.grey)),
                    if (item.isLowStock)
                      Container(
                        margin: const EdgeInsets.only(top: 4),
                        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                        decoration: BoxDecoration(
                          color: critical.withValues(alpha: 0.15),
                          borderRadius: BorderRadius.circular(4),
                        ),
                        child: const Text('Low Stock', style: TextStyle(fontSize: 11, color: critical, fontWeight: FontWeight.bold)),
                      ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}
