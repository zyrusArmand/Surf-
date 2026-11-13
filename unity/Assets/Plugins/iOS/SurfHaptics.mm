// The iPhone's own haptic taps for the game (Core/Haptics.cs): style 0 light, 1 medium, 2 heavy.
// Handheld.Vibrate can only give the long buzz, which is far too much for a trick tap.
#import <UIKit/UIKit.h>

extern "C" void SurfHapticImpact(int style)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIImpactFeedbackStyle s = style <= 0 ? UIImpactFeedbackStyleLight
                                : style == 1 ? UIImpactFeedbackStyleMedium
                                             : UIImpactFeedbackStyleHeavy;
        UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        [g prepare];
        [g impactOccurred];
    });
}
