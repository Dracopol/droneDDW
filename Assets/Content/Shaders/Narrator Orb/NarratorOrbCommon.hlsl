#ifndef NARRATOR_ORB_COMMON_INCLUDED
#define NARRATOR_ORB_COMMON_INCLUDED

// Values every orb pass shares. NarratorOrbMaterials sets them each frame; the defaults show a still, neutral orb.
float _OrbTime;    // the orb's own clock in seconds (uTime)
float _OrbRadius;  // 0.12 m (uR)
float _Breathe;    // idle breathing plus the voice swell (uBreathe)

#endif
