import 'package:Tether/providers/theme_provider.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

class SettingsPage extends StatefulWidget {
  const SettingsPage({super.key});

  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {
  double _moveSensitivity = 1;
  double _scrollSensitivity = 1;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(8.0),
          child: Column(
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    "Dark Mode",
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                  Consumer<ThemeProvider>(
                    builder: (context, value, child) => Switch(
                      value: value.isDark,
                      onChanged: value.toggleTheme,
                    ),
                  ),
                ],
              ),
              Row(
                children: [
                  const Text(
                    'Move Sensitivity',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                  Expanded(
                    child: Slider(
                      min: 0,
                      max: 2,
                      value: _moveSensitivity,
                      onChanged: (value) {
                        setState(() {
                          _moveSensitivity = value;
                        });
                      },
                    ),
                  ),
                ],
              ),
              Row(
                children: [
                  const Text(
                    'Scroll Sensitivity',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                  Expanded(
                    child: Slider(
                      min: 0,
                      max: 2,
                      value: _scrollSensitivity,
                      onChanged: (value) {
                        setState(() {
                          _scrollSensitivity = value;
                        });
                      },
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
