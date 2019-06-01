### Ifc2Xml

>Tool for extract data(both properties and geometries) from ifc file. Later using another tool I can convert ifc file to 3d-tile that can be render on [Cesium](https://cesiumjs.org/).

#### My Targets

- I want a tool that can extract both the properties and geomtries data from ifc file. IFCEngine.dll is fine but C++ is not my first choose. So I choose [XBim toolkits](https://github.com/xBimTeam).

- Secondly, I have already developed a tool that can convert my self-defined xml format of geometries to 3d-tiles that can be loaded on Cesium(**See the showcases**). So the output of this tool will be my self-defined xml format for geometries data and json for properties.

#### Usage
    It's very simple!
    ```
    ifc2xml.exe -i "ifc_file_path"
    ```

#### ShowCases
