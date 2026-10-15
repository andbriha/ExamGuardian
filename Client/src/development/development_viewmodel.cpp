#include "development_viewmodel.h"
#include "client_config.h"

DevelopmentViewModel *DevelopmentViewModel::current=nullptr;
DevelopmentViewModel::DevelopmentViewModel(QObject *parent):QObject(parent) {
    current=this;
    m_controls={"vmGate","processGate","displayGate","virtualDeviceGate","keyboardHooks","focusEnforcement"};
    m_status="Local development controls. Identity verification, recording and AI use the normal system.";
}
void DevelopmentViewModel::authenticate(const QString &) {
    m_status="Server: "+clientServerUrl()+" — normal authentication and exam workflow.";
    emit changed();
}
void DevelopmentViewModel::setOverride(const QString &control,bool enabled) {
    if(m_frozen || !m_controls.contains(control)) return;
    if(enabled && !m_active.contains(control))m_active.append(control);
    if(!enabled)m_active.removeAll(control);
    m_active.sort();emit changed();
}
void DevelopmentViewModel::reset(){if(m_frozen)return;m_active.clear();emit changed();}
void DevelopmentViewModel::enableAll(){if(m_frozen)return;m_active=m_controls;m_active.sort();emit changed();}
void DevelopmentViewModel::started(int id) {
    m_frozen=true;m_status="Attempt "+QString::number(id)+" — local controls locked until the client is restarted.";
    emit changed();
}
