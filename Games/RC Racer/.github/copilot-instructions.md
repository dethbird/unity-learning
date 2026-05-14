# Copilot Instructions

## Project Guidelines
- For the RC car controller, the handbrake should reduce Y-axis rotational speed to help slow down and reorient through tight curves, rather than adding extra yaw rotation.
- For the RC racer camera, the handbrake should tighten/zoom in around the car rather than widen into the turn, and boost should show more velocity look-ahead ahead of the car.
- For the RC racer, motor force should apply only when grounded, and landing impact should zero vertical velocity on touchdown to absorb bounce. Use extra gravity plus speed-based downforce to improve airborne feel and keep the car planted on ramps, with X rotation allowed and Z rotation frozen. Airborne gravity should bring the car down, and landing damping should absorb bounce after touchdown rather than adding more gravity.