# OccurrenceSystem

Soulution name : OccurrenceSystem_1_0_0_0
simply import it to your onpromise crm

---
the other files are controller Plug-in
---
1-Goto Plugin-Registration and find PL_Occurrence,
2-Create new step with Create Message on new_occurrence Entity (Sync)
3-Create new step with Update Message on new_occurrence(Triggered on statuscode) Entity (Async)
