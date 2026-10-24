#include <QCoreApplication>
#include "development_viewmodel.h"
int main(int argc,char **argv) {
    QCoreApplication app(argc,argv);
    DevelopmentViewModel dev;
    if(!dev.available() || dev.controls().size()!=7 || !dev.active().isEmpty())return 1;
    dev.setOverride("vmGate",true);
    if(!dev.bypass("vmGate"))return 2;
    dev.authenticate("ordinary-login");
    if(!dev.bypass("vmGate"))return 3;
    dev.setOverride("biometricAdmission",true);
    if(!dev.bypass("biometricAdmission"))return 4;
    dev.started(1);dev.reset();dev.setOverride("biometricAdmission",false);
    if(!dev.bypass("biometricAdmission"))return 6;
    if(!dev.bypass("vmGate") || !dev.frozen())return 5;
    return 0;
}
