// Where this copy of Last Tide came from (Telemetry.cs marks TestFlight builds' statistics as test data).
#import <Foundation/Foundation.h>

extern "C"
{
    // A TestFlight (or development) install carries a sandbox receipt; an App Store one doesn't.
    bool LTIsTestFlight()
    {
        NSURL *receipt = [[NSBundle mainBundle] appStoreReceiptURL];
        return receipt != nil && [[receipt lastPathComponent] isEqualToString:@"sandboxReceipt"];
    }
}
