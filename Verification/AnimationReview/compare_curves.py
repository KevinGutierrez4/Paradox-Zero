from pathlib import Path
import re
root=Path('Assets/OneMoreTime/Generated_Hacha_Final 2/Animations/Mage')
for state in ['Idle','Walk','Attack']:
    text=(root/(state+'.anim')).read_text()
    print(state)
    for prop in ['m_EulerCurves','m_PositionCurves']:
        section=text.split('  '+prop+':')[1].split('\n  m_')[0]
        for match in re.finditer(r'  - curve:\n(.*?)    path: (.*?)\n',section,re.S):
            if 'Leg' not in match[2]: continue
            values=re.findall(r'        value: (.*?)\n',match[1])
            print(prop,match[2],values[0], values[len(values)//2], values[-1])
