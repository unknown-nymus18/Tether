import 'package:flutter/material.dart';

class SensitivityProviders with ChangeNotifier {
  double _moveSensitivity = 1;
  double _scrollSensitivity = 1;

  double get moveSensitivity => _moveSensitivity;

  double get scrollSensitivity => _scrollSensitivity;

  void changeMoveSensitivity(double value) {
    if (value > 2 || value < 0) return;
    _moveSensitivity = value;
  }

  void changeScrollSensitivity(double value) {
    if (value > 2 || value < 0) return;
    _scrollSensitivity = value;
  }

  void reset() {
    _scrollSensitivity = 1;
    _moveSensitivity = 1;
  }
}
