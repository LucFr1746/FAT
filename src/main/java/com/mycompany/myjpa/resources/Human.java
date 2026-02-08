/*
 * Click nbfs://nbhost/SystemFileSystem/Templates/Licenses/license-default.txt to change this license
 * Click nbfs://nbhost/SystemFileSystem/Templates/Classes/Class.java to edit this template
 */
package com.mycompany.myjpa.resources;
import jakarta.persistence.Table;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import java.time.LocalDate;
@Entity
@Table (name = "Humans")
public class Human {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private int id;
    
    private String humancode;
    private String humanname;
    private String humandob;

    public Human() {
    }

    public int getId() {
        return id;
    }

    public void setId(int id) {
        this.id = id;
    }

    public String getHumancode() {
        return humancode;
    }

    public void setHumancode(String humancode) {
        this.humancode = humancode;
    }

    public String getHumanname() {
        return humanname;
    }

    public void setHumanname(String humanname) {
        this.humanname = humanname;
    }

    public String getHumandob() {
        return humandob;
    }

    public void setHumandob(String humandob) {
        this.humandob = humandob;
    }

    @Override
    public String toString() {
        return "Human{" + "id=" + id + ", humancode=" + humancode + ", humanname=" + humanname + ", humandob=" + humandob + '}';
    }
    
    
}
