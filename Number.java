/*
 * Click nbfs://nbhost/SystemFileSystem/Templates/Licenses/license-default.txt to change this license
 * Click nbfs://nbhost/SystemFileSystem/Templates/Classes/Class.java to edit this template
 */
package com.mycompany.exercise.resources;

import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

@Entity
@Table (name="Calculator")
public class Number {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private int soA;
    
    private int soB;

    public Number() {
    }

    public int getSoA() {
        return soA;
    }

    public void setSoA(int soA) {
        this.soA = soA;
    }

    public int getSoB() {
        return soB;
    }

    public void setSoB(int soB) {
        this.soB = soB;
    }

    @Override
    public String toString() {
        return "Number{" + "soA=" + soA + ", soB=" + soB + '}';
    }
    
}
