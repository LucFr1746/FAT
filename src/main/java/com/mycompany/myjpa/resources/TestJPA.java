/*
 * Click nbfs://nbhost/SystemFileSystem/Templates/Licenses/license-default.txt to change this license
 * Click nbfs://nbhost/SystemFileSystem/Templates/Classes/Class.java to edit this template
 */
package com.mycompany.myjpa.resources;

/**
 *
 * @author Admin
 */
public class TestJPA {
    public static void main(String[] args) {
            HumanDao hmd = new HumanDao();
            Human hm = new Human();
//            hm.setHumanname("ABC");
//            hm.setHumancode("hm01");
//            hm.setHumandob("10-10-2000");
//
//            hmd.save(hm);
    Human hm1 = hmd.findHumanById(1);
        System.out.println("human: " + hm1.toString());
    }
}
